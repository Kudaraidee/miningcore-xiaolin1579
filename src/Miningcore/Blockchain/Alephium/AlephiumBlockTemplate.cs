using System.Numerics;
using Miningcore.Serialization;
using Newtonsoft.Json;

namespace Miningcore.Blockchain.Alephium;

public class AlephiumBlockTemplate
{
    public string JobId { get; set; } = null;
    
    public ulong Height { get; set; }
    
    public DateTime Timestamp { get; set; }
    
    public int FromGroup { get; set; } = 0;
    
    public int ToGroup { get; set; } = 3;
    
    public string HeaderBlob { get; set; }
    
    public string TxsBlob { get; set; }
    
    public string TargetBlob { get; set; }
    
    public int ChainIndex { get; set; }
}