using System;
using Autofac;
using AutoMapper;
using Miningcore.Blockchain.Alephium.Configuration;
using Miningcore.Configuration;
using Miningcore.Extensions;
using Miningcore.Messaging;
using Miningcore.Mining;
using Miningcore.Payments;
using Miningcore.Persistence;
using Miningcore.Persistence.Model;
using Miningcore.Persistence.Repositories;
using Miningcore.Time;
using Miningcore.Util;
using Block = Miningcore.Persistence.Model.Block;
using Contract = Miningcore.Contracts.Contract;
using static Miningcore.Util.ActionUtils;

namespace Miningcore.Blockchain.Alephium;

[CoinFamily(CoinFamily.Alephium)]
public class AlephiumPayoutHandler : PayoutHandlerBase,
    IPayoutHandler
{
    public AlephiumPayoutHandler(
        IComponentContext ctx,
        IConnectionFactory cf,
        IMapper mapper,
        IShareRepository shareRepo,
        IBlockRepository blockRepo,
        IBalanceRepository balanceRepo,
        IPaymentRepository paymentRepo,
        IMasterClock clock,
        IMessageBus messageBus) :
        base(cf, mapper, shareRepo, blockRepo, balanceRepo, paymentRepo, clock, messageBus)
    {
        Contract.RequiresNonNull(ctx);
        Contract.RequiresNonNull(balanceRepo);
        Contract.RequiresNonNull(paymentRepo);

        this.ctx = ctx;
    }

    protected readonly IComponentContext ctx;
    protected AlephiumClient alephiumClient;
    private string network;
    private AlephiumPaymentProcessingConfigExtra extraPoolPaymentProcessingConfig;

    protected override string LogCategory => "Alephium Payout Handler";
    
    #region IPayoutHandler
    
    public virtual async Task ConfigureAsync(ClusterConfig cc, PoolConfig pc, CancellationToken ct)
    {
        Contract.RequiresNonNull(pc);

        poolConfig = pc;
        clusterConfig = cc;
        extraPoolPaymentProcessingConfig = pc.PaymentProcessing.Extra.SafeExtensionDataAs<AlephiumPaymentProcessingConfigExtra>();
        
        logger = LogUtil.GetPoolScopedLogger(typeof(AlephiumPayoutHandler), pc);

        alephiumClient = AlephiumClientFactory.CreateClient(pc, cc, null);
        
        var infosChainParams = await Guard(() => alephiumClient.GetInfosChainParamsAsync(ct),
            ex=> ReportAndRethrowApiError("Failed to get key params", ex));
        
        switch(infosChainParams?.NetworkId)
        {
            case 0:
                network = "mainnet";
                break;
            case 1:
                network = "testnet";
                break;
            case 4:
                network = "devnet";
                break;
            default:
                throw new PaymentException($"Unsupport network type '{infosChainParams?.NetworkId}'");
        }
    }
    
    public virtual async Task<Block[]> ClassifyBlocksAsync(IMiningPool pool, Block[] blocks, CancellationToken ct)
    {
        Contract.RequiresNonNull(poolConfig);
        Contract.RequiresNonNull(blocks);

        if(blocks.Length == 0)
            return blocks;

        var coin = poolConfig.Template.As<AlephiumCoinTemplate>();
        var pageSize = 100;
        var pageCount = (int) Math.Ceiling(blocks.Length / (double) pageSize);
        var result = new List<Block>();
        
        for(var i = 0; i < pageCount; i++)
        {
            // get a page full of blocks
            var page = blocks
                .Skip(i * pageSize)
                .Take(pageSize)
                .ToArray();
            
            for(var j = 0; j < page.Length; j++)
            {
                var block = page[j];
                
                // get block info
                var blockInfo = await Guard(() => alephiumClient.HashAsync((string) block.Hash, ct),
                    ex=> logger.Debug(ex));
                
                logger.Debug(() => $"[{LogCategory}] Block {block.BlockHeight} contains {blockInfo.Transactions.Count} transaction(s)");
                
                // get wallet miner's addresses
                var walletMinersAddresses = await Guard(() => alephiumClient.GetMinersAddressesAsync(ct),
                    ex=> logger.Debug(ex));
                
                // we only need the transaction(s) related to the block reward
                var blockRewardTransactions = blockInfo.Transactions
                    .Where(x => x.Unsigned.Inputs.Count < 1)
                    .ToList();
                
                logger.Debug(() => $"[{LogCategory}] Block {block.BlockHeight} contains {blockRewardTransactions.Count} transaction(s) related to the block reward");
                
                // update real blockHeight from chain if necessary
                //if (block.BlockHeight != blockInfo.Height)
                    //block.BlockHeight = blockInfo.Height;
                
                // update progress
                // Two block confirmations methods are available:
                // 1) ALPH default lock mechanism: All of the mined coins are locked for N minutes (up to +8 hours on mainnet, very short on testnet)
                // 2) Mining pool operator provides a custom block rewards lock time, this method must be ONLY USE ON TESTNET in order to mimic MAINNET
                if(extraPoolPaymentProcessingConfig?.BlockRewardsLockTime == null)
                {
                    logger.Info(() => $"[{LogCategory}] Block {block.BlockHeight} uses the default block reward lock mechanism for minimum confirmations calculation");
                    
                    decimal transactionsLockTime = 0;
                    int totalTransactionsLockTime = 0;
                    foreach (var blockTransactionLockTime in blockRewardTransactions)
                    {
                        foreach (var unsignedLockTimeFixedOutputs in blockTransactionLockTime.Unsigned.FixedOutputs)
                        {
                            // We only need the transaction(s) for our wallet miner's addresses
                            if(walletMinersAddresses.Addresses.Contains(unsignedLockTimeFixedOutputs.Address))
                            {
                                transactionsLockTime += (decimal) unsignedLockTimeFixedOutputs.LockTime;
                                totalTransactionsLockTime += 1;
                            }
                        }
                    }
                    if(totalTransactionsLockTime > 0)
                        transactionsLockTime /= totalTransactionsLockTime;
                    
                    block.ConfirmationProgress = Math.Min(1.0d, (double) ((AlephiumUtils.UnixTimeStampForApi(clock.Now) - blockInfo.Timestamp) / (transactionsLockTime - blockInfo.Timestamp)));
                }
                else
                {
                    logger.Info(() => $"[{LogCategory}] Block {block.BlockHeight} uses a custom [{network}] block rewards lock time: [{extraPoolPaymentProcessingConfig?.BlockRewardsLockTime}] minute(s)");
                    
                    block.ConfirmationProgress = Math.Min(1.0d, (double) ((AlephiumUtils.UnixTimeStampForApi(clock.Now) - blockInfo.Timestamp) / ((decimal) extraPoolPaymentProcessingConfig?.BlockRewardsLockTime * 60 * 1000)));
                }
                
                result.Add(block);

                messageBus.NotifyBlockConfirmationProgress(poolConfig.Id, block, coin);
                
                var isBlockInMainChain = await Guard(() => alephiumClient.GetBlockflowIsBlockInMainChainAsync((string) block.Hash, ct),
                    ex=> logger.Debug(ex));
                
                // We lost that battle
                if(!isBlockInMainChain)
                {
                    block.Status = BlockStatus.Orphaned;
                    block.Reward = 0;
                    
                    logger.Info(() => $"[{LogCategory}] Block {block.BlockHeight} classified as orphaned because it's not the chain");

                    messageBus.NotifyBlockUnlocked(poolConfig.Id, block, coin);
                    continue;
                }
                
                // matured and spendable?
                if(block.ConfirmationProgress >= 1)
                {
                    block.Status = BlockStatus.Confirmed;
                    block.ConfirmationProgress = 1;

                    // reset block reward
                    block.Reward = 0;

                    foreach (var blockTransaction in blockRewardTransactions)
                    {
                        foreach (var unsignedFixedOutputs in blockTransaction.Unsigned.FixedOutputs)
                        {
                            // We only need the transaction(s) for our wallet miner's addresses
                            if(walletMinersAddresses.Addresses.Contains(unsignedFixedOutputs.Address))
                                block.Reward += AlephiumUtils.ConvertNumberFromApi(unsignedFixedOutputs.AttoAlphAmount) / AlephiumConstants.SmallestUnit;
                        }
                    }
                    
                    logger.Info(() => $"[{LogCategory}] Unlocked block {block.BlockHeight} worth {FormatAmount(block.Reward)}");
                    messageBus.NotifyBlockUnlocked(poolConfig.Id, block, coin);
                }
            }
        }

        return result.ToArray();
    }
    
    public virtual async Task PayoutAsync(IMiningPool pool, Balance[] balances, CancellationToken ct)
    {
        Contract.RequiresNonNull(balances);
        
        // build args
        var amounts = balances
            .Where(x => x.Amount > 0)
            .ToDictionary(x => x.Address, x => x.Amount);

        if(amounts.Count == 0)
            return;

        var balancesTotal = amounts.Sum(x => x.Value);
        
        logger.Info(() => $"[{LogCategory}] Paying {FormatAmount(balances.Sum(x => x.Amount))} to {balances.Length} addresses");
        
        // ALPH only allows multiple recipients per transaction if they belong to the same group
        // So we must validate the addresses and consolidate them together in their respective group
        var groupingAmounts = new List<KeyValuePair<string, decimal>>[]
        {
            new List<KeyValuePair<string, decimal>>(),
            new List<KeyValuePair<string, decimal>>(),
            new List<KeyValuePair<string, decimal>>(),
            new List<KeyValuePair<string, decimal>>(),
        };
        
        logger.Info(() => $"[{LogCategory}] Validating addresses...");
        foreach(var pair in amounts)
        {
            logger.Debug(() => $"[{LogCategory}] Address {pair.Key} with amount [{FormatAmount(pair.Value)}]");
            var validity = await Guard(() => alephiumClient.GetAddressesAddressGroupAsync(pair.Key, ct));

            if(validity == null || !(validity?.Group1 >= 0))
                logger.Warn(()=> $"[{LogCategory}] Address {pair.Key} is not valid!");
            else
            {
                logger.Debug(() => $"[{LogCategory}] Address {pair.Key} belongs to group [{validity.Group1}]");
                groupingAmounts[validity.Group1].Add(pair);
            }
        }
        
        retry:
            // get wallet status
            var status = await alephiumClient.NameAsync(extraPoolPaymentProcessingConfig.WalletName, ct);

            // unlock wallet
            if(status.Locked)
                await UnlockWallet(ct);

            // get wallet addresses
            var walletAddresses = await alephiumClient.NameAddressesAsync(extraPoolPaymentProcessingConfig.WalletName, ct);
            if (walletAddresses?.Addresses1.Count < 4)
            {
                logger.Warn(() => $"[{LogCategory}] Pool payment wallet name: {extraPoolPaymentProcessingConfig.WalletName} must have 4 miner's addresses. Please fix it");
                return;
            }

            // get balance
            var walletBalances = await alephiumClient.NameBalancesAsync(extraPoolPaymentProcessingConfig.WalletName, ct);
            var walletBalanceTotal = AlephiumUtils.ConvertNumberFromApi(walletBalances.TotalBalance) / AlephiumConstants.SmallestUnit;
            var walletBalanceLocked = walletBalances.Balances1
                .Sum(x => (AlephiumUtils.ConvertNumberFromApi(x.LockedBalance) / AlephiumConstants.SmallestUnit));
            var walletBalanceAvailable = walletBalanceTotal - walletBalanceLocked;

            logger.Info(() => $"[{LogCategory}] Current wallet balance - Total: [{FormatAmount(walletBalanceTotal)}] - Locked: [{FormatAmount(walletBalanceLocked)}] - Available: [{FormatAmount(walletBalanceAvailable)}]");

            // bail if balance does not satisfy payments
            if(walletBalanceAvailable < balancesTotal)
            {
                logger.Warn(() => $"[{LogCategory}] Wallet balance currently short of {FormatAmount(balancesTotal - walletBalanceAvailable)}. Will try again");
                return;
            }

            // check if any pool address has enough funds to cover the transaction
            var anyPoolAddress = walletBalances.Balances1
                .Where(x => ((AlephiumUtils.ConvertNumberFromApi(x.Balance) / AlephiumConstants.SmallestUnit) - (AlephiumUtils.ConvertNumberFromApi(x.LockedBalance) / AlephiumConstants.SmallestUnit)) >= balancesTotal)
                .FirstOrDefault();
            // No pool wallet address can cover that transaction
            if(string.IsNullOrEmpty(anyPoolAddress?.Address))
            {
                logger.Warn(() => $"[{LogCategory}] No pool wallet address can cover that transaction");

                // we need to move funds between two addresses which hold the majority of coins, in order to allow payouts to resume
                var wealthyPoolAddress = walletBalances.Balances1
                    .OrderByDescending(x => (AlephiumUtils.ConvertNumberFromApi(x.Balance) / AlephiumConstants.SmallestUnit) - (AlephiumUtils.ConvertNumberFromApi(x.LockedBalance) / AlephiumConstants.SmallestUnit))
                    .Take(2)
                    .ToArray();

                // all available funds have already been moved to one address
                if(((AlephiumUtils.ConvertNumberFromApi(wealthyPoolAddress[1].Balance) / AlephiumConstants.SmallestUnit) - (AlephiumUtils.ConvertNumberFromApi(wealthyPoolAddress[1].LockedBalance) / AlephiumConstants.SmallestUnit)) <= 0)
                {
                    logger.Info(() => $"[{LogCategory}] All available funds have already been moved to pool wallet address {wealthyPoolAddress[0].Address} [{FormatAmount(AlephiumUtils.ConvertNumberFromApi(wealthyPoolAddress[0].Balance) / AlephiumConstants.SmallestUnit)}]");
                    return;
                }

                logger.Info(() => $"[{LogCategory}] We will now move the funds from pool wallet address {wealthyPoolAddress[1].Address} - Total: [{FormatAmount(AlephiumUtils.ConvertNumberFromApi(wealthyPoolAddress[1].Balance) / AlephiumConstants.SmallestUnit)}] - Locked: [{FormatAmount(AlephiumUtils.ConvertNumberFromApi(wealthyPoolAddress[1].LockedBalance) / AlephiumConstants.SmallestUnit)}] - Available: [{FormatAmount((AlephiumUtils.ConvertNumberFromApi(wealthyPoolAddress[1].Balance) / AlephiumConstants.SmallestUnit) - (AlephiumUtils.ConvertNumberFromApi(wealthyPoolAddress[1].LockedBalance) / AlephiumConstants.SmallestUnit))}] - to pool wallet address {wealthyPoolAddress[0].Address} - Total: [{FormatAmount(AlephiumUtils.ConvertNumberFromApi(wealthyPoolAddress[0].Balance) / AlephiumConstants.SmallestUnit)}] - Locked: [{FormatAmount(AlephiumUtils.ConvertNumberFromApi(wealthyPoolAddress[0].LockedBalance) / AlephiumConstants.SmallestUnit)}] - Available: [{FormatAmount((AlephiumUtils.ConvertNumberFromApi(wealthyPoolAddress[0].Balance) / AlephiumConstants.SmallestUnit) - (AlephiumUtils.ConvertNumberFromApi(wealthyPoolAddress[0].LockedBalance) / AlephiumConstants.SmallestUnit))}]");

                var bodyChangeWealthyActiveAddress = new ChangeActiveAddress
                {
                    Address = wealthyPoolAddress[1].Address,
                };

                await Guard(() => alephiumClient.NameChangeActiveAddressAsync(extraPoolPaymentProcessingConfig.WalletName, bodyChangeWealthyActiveAddress, ct), ex =>
                {
                    logger.Warn(() => $"[{LogCategory}] Change active address failed");
                });

                logger.Debug(() => $"[{LogCategory}] Pool wallet address {wealthyPoolAddress[1].Address} is now the active address");

                // get address UTXOs
                var wealthyPoolAddressUtxos = await alephiumClient.GetAddressesAddressUtxosAsync(wealthyPoolAddress[1].Address, ct);
                if(!string.IsNullOrEmpty(wealthyPoolAddressUtxos?.Warning))
                {
                    logger.Warn(() => $"[{LogCategory}] Pool wallet address: {wealthyPoolAddress[1].Address} maybe can't be used anymore: {wealthyPoolAddressUtxos.Warning}. Please fix it");
                    return;
                }

                logger.Debug(() => $"[{LogCategory}] Pool wallet address {wealthyPoolAddress[1].Address} has currently {wealthyPoolAddressUtxos.Utxos.Count} (locked+unlocked) UTXO(s)");
                logger.Debug(() => $"[{LogCategory}] Current Epoch Unix Timestamp {AlephiumUtils.UnixTimeStampForApi(clock.Now)} ms");

                // create UTXOs batch
                var inputWealthyUtxos = wealthyPoolAddressUtxos.Utxos
                    .Where(x => x.LockTime <= AlephiumUtils.UnixTimeStampForApi(clock.Now))
                    .Select(x => new OutputRef
                    {
                        Hint = x.Ref.Hint,
                        Key = x.Ref.Key,
                    })
                    .ToArray();

                logger.Debug(() => $"[{LogCategory}] Pool wallet address {wealthyPoolAddress[1].Address} has currently {inputWealthyUtxos.Length} (unlocked) UTXO(s)");

                // calculate gas amount for transaction
                // ALPH Gas computation - https://wiki.alephium.org/integration/exchange#gas-computation
                var inputWealthyGas = AlephiumConstants.GasPerInput * inputWealthyUtxos.Length;
                var outputWealthyGas = AlephiumConstants.GasPerOutput;
                var wealthyTxGas = inputWealthyGas + outputWealthyGas + AlephiumConstants.TxBaseGas + AlephiumConstants.P2pkUnlockGas + AlephiumConstants.GasPerOutput;
                var wealthyEstimatedGasAmount = Math.Max(AlephiumConstants.MinGasPerTx, wealthyTxGas);
                if(wealthyEstimatedGasAmount > AlephiumConstants.MaxGasPerTx)
                {
                    logger.Warn(() => $"[{LogCategory}] Estimated necessary gas amount [{wealthyEstimatedGasAmount}] exceeds the maximum possible per transaction [{AlephiumConstants.MaxGasPerTx}]. We are in a serious pickle here");
                    return;
                }

                logger.Debug(() => $"[{LogCategory}] Estimated necessary gas amount: {wealthyEstimatedGasAmount}");

                var destinationSweep = new Sweep
                {
                    ToAddress = wealthyPoolAddress[0].Address,
                    GasAmount = wealthyEstimatedGasAmount,
                };

                var txSweep = await Guard(() => alephiumClient.NameSweepActiveAddressAsync(extraPoolPaymentProcessingConfig.WalletName, destinationSweep, ct), ex =>
                {
                    logger.Warn(() => $"[{LogCategory}] Sweep active address failed");
                });
                if(txSweep?.Results == null)
                    return;

                if(txSweep.Results.Count < 1)
                    logger.Warn(() => $"[{LogCategory}] Sweep transaction failed to return a transaction id");
                else
                    logger.Info(() => $"[{LogCategory}] Sweep transaction id: {txSweep.Results.First().TxId}");

                goto retry;
            }

            // select the pool address which has enough funds to cover the transaction
            var inputAddress = walletAddresses.Addresses1
                .Where(x => x.Address == anyPoolAddress.Address)
                .FirstOrDefault();
            if(string.IsNullOrEmpty(inputAddress?.Address))
            {
                logger.Warn(() => $"[{LogCategory}] Pool wallet address {anyPoolAddress.Address} does not exist anymore. Please fix it");
                return;
            }

            logger.Info(() => $"[{LogCategory}] Pool wallet address {inputAddress.Address} has enough funds - Total: [{FormatAmount((AlephiumUtils.ConvertNumberFromApi(anyPoolAddress.Balance) / AlephiumConstants.SmallestUnit))}] - Locked: [{FormatAmount((AlephiumUtils.ConvertNumberFromApi(anyPoolAddress.LockedBalance) / AlephiumConstants.SmallestUnit))}] - Avalaible: [{FormatAmount((AlephiumUtils.ConvertNumberFromApi(anyPoolAddress.Balance) / AlephiumConstants.SmallestUnit) - (AlephiumUtils.ConvertNumberFromApi(anyPoolAddress.LockedBalance) / AlephiumConstants.SmallestUnit))}]");

            logger.Info(() => $"[{LogCategory}] Change active address");
            var bodyChangeActiveAddress = new ChangeActiveAddress
            {
                Address = inputAddress.Address,
            };

            await Guard(() => alephiumClient.NameChangeActiveAddressAsync(extraPoolPaymentProcessingConfig.WalletName, bodyChangeActiveAddress, ct), ex =>
            {
               logger.Warn(() => $"[{LogCategory}] Change active address failed");
            });

            logger.Debug(() => $"[{LogCategory}] Pool wallet address {inputAddress.Address} is now the active address");

            Balance[] successBalances;

            decimal groupTotalBalance;

            Terminus[] batchDestinations;
            OutputRef[] inputUtxos;

            int inputGas;
            int outputGas;
            int txGas;
            int estimatedGasAmount;
            int initialTotalAddresses;
            int numberOfAddressesToRemove;

            BuildSettlement destinationsTransaction;
            Sign signTxBuild;
            SubmitSettlement submitTxSign;

            // Processing groups of addresses
            for (var j = 0; j < groupingAmounts.Length; j++)
            {
                initialTotalAddresses = groupingAmounts[j].Count;
                // Only groups containing addresses are processing
                if(initialTotalAddresses > 0)
                {
                    groupTotalBalance = groupingAmounts[j].Sum(x => x.Value);
                    logger.Info(() => $"[{LogCategory}] Processing group [{j}] containing {initialTotalAddresses} address(es), total amount [{FormatAmount(groupTotalBalance)}]");

                    logger.Info(() => $"[{LogCategory}] 1/3) Build the transaction");

                    // get address UTXOs
                    var inputAddressUtxos = await alephiumClient.GetAddressesAddressUtxosAsync(inputAddress.Address, ct);
                    if(!string.IsNullOrEmpty(inputAddressUtxos?.Warning))
                    {
                        logger.Warn(() => $"[{LogCategory}] Pool wallet address: {anyPoolAddress.Address} maybe can't be used anymore: {inputAddressUtxos.Warning}. Please fix it");
                        continue;
                    }

                    logger.Debug(() => $"[{LogCategory}] Pool wallet address {inputAddress.Address} has currently {inputAddressUtxos.Utxos.Count} (locked+unlocked) UTXO(s)");
                    logger.Debug(() => $"[{LogCategory}] Current Epoch Unix Timestamp {AlephiumUtils.UnixTimeStampForApi(clock.Now)} ms");

                    // create UTXOs batch
                    inputUtxos = inputAddressUtxos.Utxos
                        .Where(x => x.LockTime <= AlephiumUtils.UnixTimeStampForApi(clock.Now))
                        .Select(x => new OutputRef
                        {
                            Hint = x.Ref.Hint,
                            Key = x.Ref.Key,
                        })
                        .ToArray();

                    logger.Debug(() => $"[{LogCategory}] Pool wallet address {inputAddress.Address} has currently {inputUtxos.Length} (unlocked) UTXO(s)");

                    // calculate gas amount for transaction
                    // ALPH Gas computation - https://wiki.alephium.org/integration/exchange#gas-computation
                    inputGas = AlephiumConstants.GasPerInput * inputUtxos.Length;
                    outputGas = AlephiumConstants.GasPerOutput * groupingAmounts[j].Count;
                    txGas = inputGas + outputGas + AlephiumConstants.TxBaseGas + AlephiumConstants.P2pkUnlockGas + AlephiumConstants.GasPerOutput;
                    estimatedGasAmount = Math.Max(AlephiumConstants.MinGasPerTx, txGas);
                    if(estimatedGasAmount > AlephiumConstants.MaxGasPerTx)
                    {
                        logger.Warn(() => $"[{LogCategory}] Estimated necessary gas amount [{estimatedGasAmount}] exceeds the maximum possible per transaction [{AlephiumConstants.MaxGasPerTx}]. We need to remove addresses.");

                        numberOfAddressesToRemove = (int)Math.Ceiling((decimal)(estimatedGasAmount - AlephiumConstants.MaxGasPerTx) / AlephiumConstants.GasPerOutput);
                        // No can do sir
                        if(numberOfAddressesToRemove >= initialTotalAddresses)
                        {
                            logger.Warn(() => $"[{LogCategory}] We need to remove {numberOfAddressesToRemove} address(es). But we only have {initialTotalAddresses} address(es) to pay. We are in a serious pickle here.");
                            continue;
                        }

                        logger.Debug(() => $"[{LogCategory}] {numberOfAddressesToRemove} address(es) are removed");
                        // trim addresses
                        while(groupingAmounts[j].Count > (initialTotalAddresses - numberOfAddressesToRemove))
                            groupingAmounts[j].RemoveAt(groupingAmounts[j].Count - 1);

                        groupTotalBalance = groupingAmounts[j].Sum(x => x.Value);
                        logger.Info(() => $"[{LogCategory}] Group {j} containing now {groupingAmounts[j].Count} address(es), total amount [{FormatAmount(groupTotalBalance)}]");

                        estimatedGasAmount = AlephiumConstants.MaxGasPerTx;
                    }

                    logger.Debug(() => $"[{LogCategory}] Estimated necessary gas amount: {estimatedGasAmount} [{FormatAmount(((estimatedGasAmount * AlephiumConstants.DefaultGasPrice) / AlephiumConstants.SmallestUnit))}]");
                    if(extraPoolPaymentProcessingConfig?.KeepTransactionFees == true)
                        logger.Debug(() => $"[{LogCategory}] Pool does not pay the transaction fee, so each address will have its payout deducted with [{FormatAmount(((estimatedGasAmount * AlephiumConstants.DefaultGasPrice) / AlephiumConstants.SmallestUnit) / groupingAmounts[j].Count)}]");

                    // create destination batch
                    batchDestinations = groupingAmounts[j].Select(x => new Terminus
                    {
                        Address = x.Key,
                        AttoAlphAmount = AlephiumUtils.ConvertNumberForApi(((extraPoolPaymentProcessingConfig?.KeepTransactionFees == false) ? x.Value * AlephiumConstants.SmallestUnit : (x.Value * AlephiumConstants.SmallestUnit) - ((estimatedGasAmount * AlephiumConstants.DefaultGasPrice) / groupingAmounts[j].Count))),
                    }).ToArray();

                    destinationsTransaction = new BuildSettlement
                    {
                        FromPublicKey = inputAddress.PublicKey,
                        Destinations = batchDestinations,
                        Utxos = inputUtxos,
                        GasAmount = estimatedGasAmount,
                    };

                    var txBuild = await Guard(() => alephiumClient.PostTransactionsBuildAsync(destinationsTransaction, ct), ex =>
                    {
                        logger.Warn(() => $"[{LogCategory}] Build transaction failed");
                    });
                    if(string.IsNullOrEmpty(txBuild?.TxId))
                        continue;

                    logger.Debug(() => $"[{LogCategory}] Unsigned transaction {txBuild.UnsignedTx} with txId {txBuild.TxId}");

                    logger.Info(() => $"[{LogCategory}] 2/3) Sign the transaction");
                    signTxBuild = new Sign
                    {
                        Data = txBuild.TxId,
                    };

                    var txSign = await Guard(() => alephiumClient.NameSignAsync(extraPoolPaymentProcessingConfig.WalletName, signTxBuild, ct), ex =>
                    {
                        logger.Warn(() => $"[{LogCategory}] Sign transaction failed");
                    });
                    if(string.IsNullOrEmpty(txSign?.Signature))
                        continue;

                    logger.Debug(() => $"[{LogCategory}] Unsigned transaction signature {txSign.Signature}");

                    logger.Info(() => $"[{LogCategory}] 3/3) Submit signed transaction to the network");
                    submitTxSign = new SubmitSettlement
                    {
                        UnsignedTx = txBuild.UnsignedTx,
                        Signature = txSign.Signature,
                    };

                    var txSubmit = await Guard(() => alephiumClient.PostTransactionsSubmitAsync(submitTxSign, ct), ex =>
                    {
                        logger.Warn(() => $"[{LogCategory}] Submit signed transaction failed");
                    });
                    if(string.IsNullOrEmpty(txSubmit?.TxId))
                    {
                        logger.Warn(() => $"[{LogCategory}] Payment transaction failed to return a transaction id");
                        continue;
                    }

                    // payment successful
                    logger.Info(() => $"[{LogCategory}] Payment transaction id: {txSubmit.TxId}");

                    successBalances = groupingAmounts[j]
                        .Select(x => new Balance
                        {
                            PoolId = poolConfig.Id,
                            Address = x.Key,
                            Amount = x.Value,
                        })
                        .ToArray();

                    await PersistPaymentsAsync(successBalances, txSubmit.TxId);

                    NotifyPayoutSuccess(poolConfig.Id, successBalances, new[] {txSubmit.TxId}, ((estimatedGasAmount * AlephiumConstants.DefaultGasPrice) / AlephiumConstants.SmallestUnit));
                }
            }
        
        await LockWallet(ct);
    }
    
    public override double AdjustShareDifficulty(double difficulty)
    {
        return difficulty * AlephiumConstants.Pow2xDiff1TargetNumZero;
    }

    public double AdjustBlockEffort(double effort)
    {
        return effort * AlephiumConstants.Pow2xDiff1TargetNumZero;
    }
    
    #endregion // IPayoutHandler

    private class PaymentException : Exception
    {
        public PaymentException(string msg) : base(msg)
        {
        }
    }

    private void ReportAndRethrowApiError(string action, Exception ex, bool rethrow = true)
    {
        var error = ex.Message;

        if(ex is AlephiumApiException apiException)
            error = apiException.Response;

        logger.Warn(() => $"{action}: {error}");

        if(rethrow)
            throw ex;
    }

    private async Task UnlockWallet(CancellationToken ct)
    {
        logger.Info(() => $"[{LogCategory}] Unlocking wallet: {extraPoolPaymentProcessingConfig.WalletName}");

        var walletPassword = extraPoolPaymentProcessingConfig.WalletPassword ?? string.Empty;

        await Guard(() => alephiumClient.NameUnlockAsync(extraPoolPaymentProcessingConfig.WalletName, new WalletUnlock {Password = walletPassword}, ct), ex =>
        {
            if (ex is AlephiumApiException apiException)
            {
                var error = apiException.Response;

                if (error != null && !error.ToLower().Contains("already unlocked"))
                    throw new PaymentException($"Failed to unlock wallet: {error}");
            }

            else
                throw ex;
        });

        logger.Info(() => $"[{LogCategory}] Wallet: {extraPoolPaymentProcessingConfig.WalletName} unlocked");
    }

    private async Task LockWallet(CancellationToken ct)
    {
        logger.Info(() => $"[{LogCategory}] Locking wallet: {extraPoolPaymentProcessingConfig.WalletName}");

        await Guard(() => alephiumClient.NameLockAsync(extraPoolPaymentProcessingConfig.WalletName, ct),
            ex => ReportAndRethrowApiError("Failed to lock wallet", ex));

        logger.Info(() => $"[{LogCategory}] Wallet: {extraPoolPaymentProcessingConfig.WalletName} is locked");
    }
}