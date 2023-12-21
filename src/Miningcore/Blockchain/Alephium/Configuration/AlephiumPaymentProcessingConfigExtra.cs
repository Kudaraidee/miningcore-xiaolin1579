namespace Miningcore.Blockchain.Alephium.Configuration;

public class AlephiumPaymentProcessingConfigExtra
{
    /// <summary>
    /// Name of the wallet to use
    /// </summary>
    public string WalletName { get; set; }
    
    /// <summary>
    /// Password for unlocking wallet
    /// </summary>
    public string WalletPassword { get; set; }
    
    /// <summary>
    /// WARNING: DO NOT USE ON MAINNET UNLESS YOU KNOW WHAT YOU ARE DOING
    /// The sole purpose of that option is to mimic the long (mainnet) block rewards "lock time" mechanism on TESTNET!!!
    /// </summary>
    public long? BlockRewardsLockTime { get; set; }
    
    /// <summary>
    /// True to exempt transaction fees from miner rewards
    /// </summary>
    public bool KeepTransactionFees { get; set; }
}