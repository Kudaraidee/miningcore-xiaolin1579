namespace Miningcore.Blockchain.Alephium;

public enum AlephiumStratumError
{
    JobNotFound = 20,
    InvalidJobChainIndex = 21,
    InvalidWorker = 22,
    InvalidNonce = 23,
    DuplicatedShare = 24,
    LowDifficultyShare = 25,
    InvalidBlockChainIndex = 26,
    MinusOne = -1
}

public class AlephiumStratumException : Exception
{
    public AlephiumStratumException(AlephiumStratumError code, string message) : base(message)
    {
        Code = code;
    }

    public AlephiumStratumError Code { get; set; }
}
