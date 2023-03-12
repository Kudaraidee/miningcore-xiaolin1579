namespace Miningcore.Blockchain.Evrmore;

public class EvrmoreExtraNonceProvider : ExtraNonceProviderBase
{
    public EvrmoreExtraNonceProvider(string poolId, byte? clusterInstanceId) : base(poolId, EvrmoreConstants.ExtranoncePlaceHolderLength, clusterInstanceId)
    {
    }
}
