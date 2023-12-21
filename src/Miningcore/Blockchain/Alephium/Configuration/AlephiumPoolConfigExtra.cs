using Miningcore.Configuration;

namespace Miningcore.Blockchain.Alephium.Configuration;

public class AlephiumPoolConfigExtra
{
    /// <summary>
    /// Maximum number of tracked jobs.
    /// Default: 8
    /// </summary>
    public int? MaxActiveJobs { get; set; }
    
    /// <summary>
    /// Maximum size of buffer when receiving job message
    /// Default: 131072
    /// </summary>
    public int? SocketJobMessageBufferSize { get; set; }

    public int? ExtraNonce1Size { get; set; }
}