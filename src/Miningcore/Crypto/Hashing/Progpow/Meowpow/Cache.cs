using System.Diagnostics;
using Miningcore.Blockchain.Progpow;
using Miningcore.Contracts;
using Miningcore.Extensions;
using Miningcore.Messaging;
using Miningcore.Native;
using Miningcore.Notifications.Messages;
using NLog;

namespace Miningcore.Crypto.Hashing.Progpow.Meowpow;

[Identifier("meowpow")]
public class Cache : IProgpowCache
{
    public Cache(int epoch)
    {
        Epoch = epoch;
        LastUsed = DateTime.Now;
    }

    private IntPtr handle = IntPtr.Zero;
    private bool isGenerated = false;
    private readonly object genLock = new();
    internal static IMessageBus messageBus;
    public int Epoch { get; }
    public byte[] SeedHash { get; set; }
    public DateTime LastUsed { get; set; }

    public void Dispose()
    {
        if(handle != IntPtr.Zero)
        {
            MeowPow.DestroyContext(handle);
            handle = IntPtr.Zero;
        }
    }

    public async Task GenerateAsync(ILogger logger)
    {
        await Task.Run(() =>
        {
            lock(genLock)
            {
                if(!isGenerated)
                {

                    var started = DateTime.Now;
                    logger.Debug(() => $"Generating cache for epoch {Epoch}");

                    handle = MeowPow.CreateContext(Epoch);

                    logger.Debug(() => $"Done generating cache for epoch {Epoch} after {DateTime.Now - started}");
                    isGenerated = true;

                    // get the seed hash for this epoch
                    var res = MeowPow.calculate_epoch_seed(Epoch);
                    SeedHash = res.bytes;
                    logger.Info(() => $"Seed hash for epoch {Epoch} is {SeedHash.ToHexString()}");
                }
            }
        });
    }

    public unsafe bool Compute(ILogger logger, int blockNumber, byte[] hash, ulong nonce, out byte[] mixDigest, out byte[] result)
    {
        Contract.RequiresNonNull(hash);

        var sw = Stopwatch.StartNew();

        mixDigest = null;
        result = null;

        var value = new MeowPow.Ethash_result();

        var inputHash = new MeowPow.Ethash_hash256();
        inputHash.bytes = hash;

        fixed(byte* input = hash)
        {
            value = MeowPow.hash(handle, blockNumber, ref inputHash, nonce);
        }

        if(value.final_hash.bytes == null)
        {
            logger.Error(() => $"MeowPow.hash returned null");
            return false;
        }

        mixDigest = value.mix_hash.bytes;
        result = value.final_hash.bytes;

        messageBus?.SendTelemetry("Meowpow", TelemetryCategory.Hash, sw.Elapsed, true);

        return true;
    }
}