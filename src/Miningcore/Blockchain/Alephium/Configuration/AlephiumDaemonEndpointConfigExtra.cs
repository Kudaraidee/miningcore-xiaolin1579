namespace Miningcore.Blockchain.Alephium.Configuration;

public class AlephiumDaemonEndpointConfigExtra
{
    /// <summary>
    /// The Alephium Node's API key in clear-text - not the hash
    /// </summary>
    public string ApiKey { get; set; }
    
    /// <summary>
    /// The Alephium Node's Miner API Port
    /// </summary>
    public int MinerApiPort  { get; set; }
}