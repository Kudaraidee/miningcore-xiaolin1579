using Miningcore.Mining;

namespace Miningcore.Blockchain.Alephium;

public class AlephiumWorkerSubmitParams
{
    public string JobId { get; init; }
    public int FromGroup { get; init; }
    public int ToGroup { get; init; }
    public string Nonce { get; init; }
    public string Worker { get; init; }
}

public class AlephiumWorkerContext : WorkerContextBase
{
    /// <summary>
    /// Usually a wallet address
    /// </summary>
    public string Miner { get; set; }

    /// <summary>
    /// Arbitrary worker identififer for miners using multiple rigs
    /// </summary>
    public string Worker { get; set; }

    /// <summary>
    /// Unique value assigned per worker
    /// </summary>
    public string ExtraNonce1 { get; set; }
}