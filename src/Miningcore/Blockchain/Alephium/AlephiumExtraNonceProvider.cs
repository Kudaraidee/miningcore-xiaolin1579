namespace Miningcore.Blockchain.Alephium;

public class AlephiumExtraNonceProvider : ExtraNonceProviderBase
{
    public AlephiumExtraNonceProvider(string poolId, int size, byte? clusterInstanceId) : base(poolId, size, clusterInstanceId)
    {
    }
}