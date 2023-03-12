using Miningcore.Blockchain.Evrmore;
using NLog;

namespace Miningcore.Crypto.Hashing.Evrprogpow;

public class EvrProgpowLight : IDisposable
{
    public void Setup(int numCaches)
    {
        this.numCaches = numCaches;
    }

    private int numCaches; // Maximum number of caches to keep before eviction (only init, don't modify)
    private readonly object cacheLock = new();
    private readonly Dictionary<int, EvrProgpowCache> caches = new();
    private EvrProgpowCache future;
    public string AlgoName { get; } = "EvrProgpow";

    public void Dispose()
    {
        foreach(var value in caches.Values)
            value.Dispose();
    }

    public async Task<EvrProgpowCache> GetCacheAsync(ILogger logger, int block)
    {
        var epoch = block / EvrmoreConstants.EpochLength;
        EvrProgpowCache result;

        lock(cacheLock)
        {
            if(numCaches == 0)
                numCaches = 3;

            if(!caches.TryGetValue(epoch, out result))
            {
                // No cached EvrProgpowCache, evict the oldest if the EvrProgpowCache limit was reached
                while(caches.Count >= numCaches)
                {
                    var toEvict = caches.Values.OrderBy(x => x.LastUsed).First();
                    var key = caches.First(pair => pair.Value == toEvict).Key;
                    var epochToEvict = toEvict.Epoch;

                    logger.Info(() => $"Evicting EvrProgpowCache for epoch {epochToEvict} in favour of epoch {epoch}");
                    toEvict.Dispose();
                    caches.Remove(key);
                }

                // If we have the new EvrProgpowCache pre-generated, use that, otherwise create a new one
                if(future != null && future.Epoch == epoch)
                {
                    logger.Debug(() => $"Using pre-generated EvrProgpowCache for epoch {epoch}");

                    result = future;
                    future = null;
                }

                else
                {
                    logger.Info(() => $"No pre-generated EvrProgpowCache available, creating new for epoch {epoch}");
                    result = new EvrProgpowCache(epoch);
                }

                caches[epoch] = result;
            }

            // If we used up the future EvrProgpowCache, or need a refresh, regenerate
            else if(future == null || future.Epoch <= epoch)
            {
                logger.Info(() => $"Pre-generating EvrProgpowCache for epoch {epoch + 1}");
                future = new EvrProgpowCache(epoch + 1);

#pragma warning disable 4014
                future.GenerateAsync(logger);
#pragma warning restore 4014
            }

            result.LastUsed = DateTime.Now;
        }

        // get/generate current one
        await result.GenerateAsync(logger);

        return result;
    }
}
