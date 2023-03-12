using Miningcore.Mining;

namespace Miningcore.Blockchain.Evrmore;

public class EvrmoreWorkerContext : WorkerContextBase
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

    private List<EvrmoreWorkerJob> ValidJobs { get; } = new();

    public void AddJob(EvrmoreWorkerJob job)
    {
        ValidJobs.Insert(0, job);

        while(ValidJobs.Count > 4)
            ValidJobs.RemoveAt(ValidJobs.Count - 1);
    }

    public EvrmoreWorkerJob FindJob(string jobId)
    {
        return ValidJobs.FirstOrDefault(x => x.Id == jobId);
    }
}
