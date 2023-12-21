using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Numerics;
using Autofac;
using AutoMapper;
using Microsoft.IO;
using Miningcore.Blockchain.Bitcoin;
using Miningcore.Blockchain.Alephium.Configuration;
using Miningcore.Configuration;
using Miningcore.Extensions;
using Miningcore.JsonRpc;
using Miningcore.Messaging;
using Miningcore.Mining;
using Miningcore.Nicehash;
using Miningcore.Notifications.Messages;
using Miningcore.Persistence;
using Miningcore.Persistence.Repositories;
using Miningcore.Stratum;
using Miningcore.Time;
using Miningcore.Util;
using Newtonsoft.Json;
using static Miningcore.Util.ActionUtils;

namespace Miningcore.Blockchain.Alephium;

[CoinFamily(CoinFamily.Alephium)]
public class AlephiumPool : PoolBase
{
    public AlephiumPool(IComponentContext ctx,
        JsonSerializerSettings serializerSettings,
        IConnectionFactory cf,
        IStatsRepository statsRepo,
        IMapper mapper,
        IMasterClock clock,
        IMessageBus messageBus,
        RecyclableMemoryStreamManager rmsm,
        NicehashService nicehashService) :
        base(ctx, serializerSettings, cf, statsRepo, mapper, clock, messageBus, rmsm, nicehashService)
    {
    }

    protected AlephiumJobParams currentJobParams;
    protected AlephiumJobManager manager;
    private AlephiumPoolConfigExtra extraPoolConfig;
    private AlephiumCoinTemplate coin;

    protected virtual async Task OnSubscribeAsync(StratumConnection connection, Timestamped<JsonRpcRequest> tsRequest)
    {
        var request = tsRequest.Value;

        if(request.Id == null)
            throw new StratumException(StratumError.MinusOne, "missing request id");

        var context = connection.ContextAs<AlephiumWorkerContext>();
        var requestParams = request.ParamsAs<string[]>();

        await connection.RespondAsync(connection.ConnectionId, request.Id);

        // setup worker context
        context.IsSubscribed = true;
        context.UserAgent = requestParams.FirstOrDefault()?.Trim();
    }

    protected virtual async Task OnAuthorizeAsync(StratumConnection connection, Timestamped<JsonRpcRequest> tsRequest, CancellationToken ct)
    {
        var request = tsRequest.Value;

        if(request.Id == null)
            throw new StratumException(StratumError.MinusOne, "missing request id");

        var context = connection.ContextAs<AlephiumWorkerContext>();
        var requestParams = request.ParamsAs<string[]>();
        var workerValue = requestParams?.Length > 0 ? requestParams[0] : null;
        var password = requestParams?.Length > 1 ? requestParams[1] : null;
        var passParts = password?.Split(PasswordControlVarsSeparator);

        // extract worker/miner
        var split = workerValue?.Split('.');
        var minerName = split?.FirstOrDefault()?.Trim();
        var workerName = split?.Skip(1).FirstOrDefault()?.Trim() ?? string.Empty;

        // assumes that minerName is an address
        context.IsAuthorized = await manager.ValidateAddress(minerName, ct);
        context.Miner = minerName;
        context.Worker = workerName;

        if(context.IsAuthorized)
        {
            // Despite having a subscribe method in their stratum RPC protocol: https://wiki.alephium.org/mining/alephium-stratum/, no mining software seems to use it, everyone just go straight to authorize, so we need to handle it somehow :/
            if(!context.IsSubscribed)
            {
                // setup worker context
                context.IsSubscribed = true;
                context.UserAgent = requestParams.FirstOrDefault()?.Trim();
            }
            
            // respond
            await connection.RespondAsync(context.IsAuthorized, request.Id);
            
            // send extranonce
            await connection.NotifyAsync(AlephiumStratumMethods.SetExtraNonce, manager.GetSubscriberData(connection));

            // log association
            logger.Info(() => $"[{connection.ConnectionId}] Authorized worker {workerValue}");

            // extract control vars from password
            var staticDiff = GetStaticDiffFromPassparts(passParts);

            // Nicehash support
            var nicehashDiff = await GetNicehashStaticMinDiff(context, coin.Name, coin.GetAlgorithmName());

            if(nicehashDiff.HasValue)
            {
                if(!staticDiff.HasValue || nicehashDiff > staticDiff)
                {
                    logger.Info(() => $"[{connection.ConnectionId}] Nicehash detected. Using API supplied difficulty of {nicehashDiff.Value}");

                    staticDiff = nicehashDiff;
                }

                else
                    logger.Info(() => $"[{connection.ConnectionId}] Nicehash detected. Using miner supplied difficulty of {staticDiff.Value}");
            }

            // Static diff
            if(staticDiff.HasValue &&
               (context.VarDiff != null && staticDiff.Value >= context.VarDiff.Config.MinDiff ||
                   context.VarDiff == null && staticDiff.Value > context.Difficulty))
            {
                context.VarDiff = null; // disable vardiff
                context.SetDifficulty(staticDiff.Value);

                logger.Info(() => $"[{connection.ConnectionId}] Setting static difficulty of {staticDiff.Value}");
            }
            
            // send intial job
            await SendJob(connection, context, currentJobParams);
        }

        else
        {
            await connection.RespondErrorAsync(StratumError.UnauthorizedWorker, "Authorization failed", request.Id, context.IsAuthorized);

            if(clusterConfig?.Banning?.BanOnLoginFailure is null or true)
            {
                // issue short-time ban if unauthorized to prevent DDos on daemon (validateaddress RPC)
                logger.Info(() => $"[{connection.ConnectionId}] Banning unauthorized worker {minerName} for {loginFailureBanTimeout.TotalSeconds} sec");

                banManager.Ban(connection.RemoteEndpoint.Address, loginFailureBanTimeout);

                Disconnect(connection);
            }
        }
    }

    protected virtual async Task OnSubmitAsync(StratumConnection connection, Timestamped<JsonRpcRequest> tsRequest, CancellationToken ct)
    {
        var request = tsRequest.Value;
        var context = connection.ContextAs<AlephiumWorkerContext>();

        try
        {
            if(request.Id == null)
                throw new StratumException(StratumError.MinusOne, "missing request id");

            // check age of submission (aged submissions are usually caused by high server load)
            var requestAge = clock.Now - tsRequest.Timestamp.UtcDateTime;

            if(requestAge > maxShareAge)
            {
                logger.Warn(() => $"[{connection.ConnectionId}] Dropping stale share submission request (server overloaded?)");
                return;
            }

            // check worker state
            context.LastActivity = clock.Now;

            // validate worker
            if(!context.IsAuthorized)
                throw new AlephiumStratumException(AlephiumStratumError.InvalidWorker, "unauthorized worker");
            else if(!context.IsSubscribed)
                throw new AlephiumStratumException(AlephiumStratumError.InvalidWorker, "not subscribed");

            var requestParams = request.ParamsAs<AlephiumWorkerSubmitParams>();

            // submit
            var share = await manager.SubmitShareAsync(connection, requestParams, ct);
            await connection.RespondAsync(true, request.Id);

            // publish
            messageBus.SendMessage(share);

            // telemetry
            PublishTelemetry(TelemetryCategory.Share, clock.Now - tsRequest.Timestamp.UtcDateTime, true);

            logger.Info(() => $"[{connection.ConnectionId}] Share accepted: D={Math.Round(share.Difficulty * AlephiumConstants.ShareMultiplier, 3)}");

            // update pool stats
            if(share.IsBlockCandidate)
                poolStats.LastPoolBlockTime = clock.Now;

            // update client stats
            context.Stats.ValidShares++;

            await UpdateVarDiffAsync(connection, false, ct);
        }

        catch(AlephiumStratumException ex)
        {
            // telemetry
            PublishTelemetry(TelemetryCategory.Share, clock.Now - tsRequest.Timestamp.UtcDateTime, false);

            // update client stats
            context.Stats.InvalidShares++;
            logger.Info(() => $"[{connection.ConnectionId}] Share rejected: {ex.Message} [{context.UserAgent}]");

            // banning
            ConsiderBan(connection, context, poolConfig.Banning);

            throw;
        }
    }

    protected virtual async Task OnNewJobAsync(AlephiumJobParams jobParams)
    {
        currentJobParams = jobParams;

        logger.Info(() => $"Broadcasting job {jobParams.JobId}");

        await Guard(() => ForEachMinerAsync(async (connection, ct) =>
        {
            var context = connection.ContextAs<AlephiumWorkerContext>();
            
            await SendJob(connection, context, currentJobParams);
        }));
    }

    private async Task SendJob(StratumConnection connection, AlephiumWorkerContext context, AlephiumJobParams jobParams)
    {
        var target = EncodeTarget(context.Difficulty);
        
        // clone job params
        var jobParamsActual = new AlephiumJobParams
        {
            JobId = jobParams.JobId,
            FromGroup = jobParams.FromGroup,
            ToGroup = jobParams.ToGroup,
            HeaderBlob = jobParams.HeaderBlob,
            TxsBlob = jobParams.TxsBlob,
            TargetBlob = target,
        };
        
        // send difficulty
        await connection.NotifyAsync(AlephiumStratumMethods.SetDifficulty, new object[] { context.Difficulty });
        
        // send job
        await connection.NotifyAsync(AlephiumStratumMethods.MiningNotify, new object[] { jobParamsActual });
    }

    public override double HashrateFromShares(double shares, double interval)
    {
        var multiplier = 16 * AlephiumConstants.Pow2xDiff1TargetNumZero;
        var result = shares * multiplier / interval;

        return result;
    }

    public override double ShareMultiplier => AlephiumConstants.ShareMultiplier;

    #region Overrides

    public override void Configure(PoolConfig pc, ClusterConfig cc)
    {
        coin = pc.Template.As<AlephiumCoinTemplate>();
        extraPoolConfig = pc.Extra.SafeExtensionDataAs<AlephiumPoolConfigExtra>();

        base.Configure(pc, cc);
    }

    protected override async Task SetupJobManager(CancellationToken ct)
    {
        var extraNonce1Size = extraPoolConfig?.ExtraNonce1Size ?? 2;

        manager = ctx.Resolve<AlephiumJobManager>(
            new TypedParameter(typeof(IExtraNonceProvider), new AlephiumExtraNonceProvider(poolConfig.Id, extraNonce1Size, clusterConfig.InstanceId)));

        manager.Configure(poolConfig, clusterConfig);

        await manager.StartAsync(ct);

        if(poolConfig.EnableInternalStratum == true)
        {
            disposables.Add(manager.Jobs
                .Select(job => Observable.FromAsync(() =>
                    Guard(()=> OnNewJobAsync(job),
                        ex=> logger.Debug(() => $"{nameof(OnNewJobAsync)}: {ex.Message}"))))
                .Concat()
                .Subscribe(_ => { }, ex =>
                {
                    logger.Debug(ex, nameof(OnNewJobAsync));
                }));

            // start with initial blocktemplate
            await manager.Jobs.Take(1).ToTask(ct);
        }

        else
        {
            // keep updating NetworkStats
            disposables.Add(manager.Jobs.Subscribe());
        }
    }

    protected override async Task InitStatsAsync(CancellationToken ct)
    {
        await base.InitStatsAsync(ct);

        blockchainStats = manager.BlockchainStats;
    }

    protected override WorkerContextBase CreateWorkerContext()
    {
        return new AlephiumWorkerContext();
    }

    protected override async Task OnRequestAsync(StratumConnection connection,
        Timestamped<JsonRpcRequest> tsRequest, CancellationToken ct)
    {
        var request = tsRequest.Value;

        try
        {
            switch(request.Method)
            {
                case AlephiumStratumMethods.Subscribe:
                    await OnSubscribeAsync(connection, tsRequest);
                    break;

                case AlephiumStratumMethods.Authorize:
                    await OnAuthorizeAsync(connection, tsRequest, ct);
                    break;

                case AlephiumStratumMethods.SubmitShare:
                    await OnSubmitAsync(connection, tsRequest, ct);
                    break;
                
                case AlephiumStratumMethods.SubmitHashrate:
                    break;
                
                default:
                    logger.Debug(() => $"[{connection.ConnectionId}] Unsupported RPC request: {JsonConvert.SerializeObject(request, serializerSettings)}");

                    await connection.RespondErrorAsync(StratumError.Other, $"Unsupported request {request.Method}", request.Id);
                    break;
            }
        }

        catch(StratumException ex)
        {
            await connection.RespondErrorAsync(ex.Code, ex.Message, request.Id, false);
        }
    }

    protected override async Task<double?> GetNicehashStaticMinDiff(WorkerContextBase context, string coinName, string algoName)
    {
        var result = await base.GetNicehashStaticMinDiff(context, coinName, algoName);

        // adjust value to fit with our target value calculation
        if(result.HasValue)
            result = result.Value / uint.MaxValue;

        return result;
    }

    protected override async Task OnVarDiffUpdateAsync(StratumConnection connection, double newDiff, CancellationToken ct)
    {
        await base.OnVarDiffUpdateAsync(connection, newDiff, ct);

        var context = connection.ContextAs<AlephiumWorkerContext>();

        if(context.ApplyPendingDifficulty())
        {
            // send job
            await SendJob(connection, context, currentJobParams);
        }
    }
    
    private string EncodeTarget(double difficulty)
    {
        return AlephiumUtils.EncodeTarget(difficulty);
    }

    #endregion // Overrides
}