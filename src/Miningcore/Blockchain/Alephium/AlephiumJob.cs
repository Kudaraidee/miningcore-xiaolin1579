using System.Globalization;
using System.Numerics;
using System.Collections.Concurrent;
using System.Text;
using Miningcore.Contracts;
using Miningcore.Crypto;
using Miningcore.Crypto.Hashing.Algorithms;
using Miningcore.Extensions;
using Miningcore.Stratum;
using Miningcore.Time;
using Miningcore.Util;
using NBitcoin;

namespace Miningcore.Blockchain.Alephium;

public class AlephiumJobParams
{
    public string JobId { get; init; }
    public int FromGroup { get; init; }
    public int ToGroup { get; init; }
    public string HeaderBlob { get; init; }
    public string TxsBlob { get; init; }
    public string TargetBlob { get; set; }
}

public class AlephiumJob
{
    protected IMasterClock clock;
    public AlephiumBlockTemplate BlockTemplate { get; private set; }
    public double Difficulty { get; private set; }
    public string JobId { get; protected set; }
    protected uint256 blockTargetValue;

    private AlephiumJobParams jobParams;
    private readonly ConcurrentDictionary<string, bool> submissions = new(StringComparer.OrdinalIgnoreCase);
    private static readonly IHashAlgorithm hasher = new Blake3();
    
    private static byte[] GetBigEndianUInt32(uint value)
    {
        byte[] bytes = new byte[4];

        bytes[0] = (byte)((value >> 24) & 0xFF);
        bytes[1] = (byte)((value >> 16) & 0xFF);
        bytes[2] = (byte)((value >> 8) & 0xFF);
        bytes[3] = (byte)(value & 0xFF);

        return bytes;
    }

    protected bool RegisterSubmit(string nonce)
    {
        var key = new StringBuilder()
            .Append(nonce)
            .ToString();

        return submissions.TryAdd(key, true);
    }
    
    public virtual byte[] SerializeCoinbase(string nonce)
    {
        var nonceBytes = (Span<byte>) nonce.HexToByteArray();
        var headerBlobBytes = (Span<byte>) BlockTemplate.HeaderBlob.HexToByteArray();
        var txsBlobBytes = (Span<byte>) BlockTemplate.TxsBlob.HexToByteArray();
        
        uint blockSize = (uint)nonceBytes.Length + (uint)headerBlobBytes.Length + (uint)txsBlobBytes.Length;
        uint messageSize = 4 + 1 + blockSize; // encodedBlockSize(4 bytes) + messageType(1 byte)
        
        using(var stream = new MemoryStream())
        {
            stream.Write(GetBigEndianUInt32(messageSize));
            stream.WriteByte(AlephiumConstants.SubmitBlockMessageType);
            stream.Write(GetBigEndianUInt32(blockSize));
            stream.Write(nonceBytes);
            stream.Write(headerBlobBytes);
            stream.Write(txsBlobBytes);

            return stream.ToArray();
        }
    }

    protected virtual Share ProcessShareInternal(StratumConnection worker, string nonce)
    {
        var context = worker.ContextAs<AlephiumWorkerContext>();
        
        var nonceBytes = (Span<byte>) nonce.HexToByteArray();
        
        // validate nonce
        if(nonceBytes.Length != AlephiumConstants.NonceLength)
            throw new AlephiumStratumException(AlephiumStratumError.InvalidNonce, "incorrect size of nonce");
        
        var headerBytes = (Span<byte>) BlockTemplate.HeaderBlob.HexToByteArray();
        
        // concat nonce and header
        Span<byte> headerNonceBytes = stackalloc byte[nonceBytes.Length + headerBytes.Length];
        nonceBytes.CopyTo(headerNonceBytes);

        headerBytes.CopyTo(headerNonceBytes[nonceBytes.Length..]);
        
        // I know, the following looks weird but it's Alephium blockHash calculation method: https://wiki.alephium.org/mining/integration/#calculating-the-blockhash
        Span<byte> tmpHashBytes = stackalloc byte[32];
        hasher.Digest(headerNonceBytes, tmpHashBytes);
        Span<byte> hashBytes = stackalloc byte[32];
        hasher.Digest(tmpHashBytes, hashBytes);
        
        var (fromGroup, toGroup) = AlephiumUtils.BlockChainIndex(hashBytes);
        // validate blockchainIndex
        if (fromGroup != BlockTemplate.FromGroup || toGroup != BlockTemplate.ToGroup)
            throw new AlephiumStratumException(AlephiumStratumError.InvalidBlockChainIndex, $"invalid block chain index, expected: ['fromGroup': {BlockTemplate.FromGroup}, 'toGroup': {BlockTemplate.ToGroup}], received['fromGroup': {fromGroup}, 'toGroup': {toGroup}]");
        
        var targetHashBytes = new Target(new BigInteger(hashBytes, true, true));
        var hashBytesValue = targetHashBytes.ToUInt256();
        
        // calc share-diff
        var shareDiff = (double) new BigRational(AlephiumConstants.Diff1Target * 1024, targetHashBytes.ToBigInteger()) / 1024;
        // diff check
        var stratumDifficulty = context.Difficulty;
        var ratio = shareDiff / stratumDifficulty;

        // check if the share meets the much harder block difficulty (block candidate)
        var isBlockCandidate = hashBytesValue <= blockTargetValue;

        // test if share meets at least workers current difficulty
        if(!isBlockCandidate && ratio < 0.99)
        {
            // check if share matched the previous difficulty from before a vardiff retarget
            if(context.VarDiff?.LastUpdate != null && context.PreviousDifficulty.HasValue)
            {
                ratio = shareDiff / context.PreviousDifficulty.Value;

                if(ratio < 0.99)
                    throw new AlephiumStratumException(AlephiumStratumError.LowDifficultyShare, $"low difficulty share ({shareDiff})");

                // use previous difficulty
                stratumDifficulty = context.PreviousDifficulty.Value;
            }

            else
                throw new AlephiumStratumException(AlephiumStratumError.LowDifficultyShare, $"low difficulty share ({shareDiff})");
        }

        var result = new Share
        {
            BlockHeight = (long) BlockTemplate.Height,
            NetworkDifficulty = Difficulty,
            Difficulty = stratumDifficulty / AlephiumConstants.ShareMultiplier
        };

        if(isBlockCandidate)
        {
            result.IsBlockCandidate = true;
            result.BlockHash = hashBytes.ToHexString();
        }

        return result;
    }

    public AlephiumJobParams GetJobParams()
    {
        return jobParams;
    }

    public virtual Share ProcessShare(StratumConnection worker, string nonce)
    {
        Contract.RequiresNonNull(worker);
        Contract.Requires<ArgumentException>(!string.IsNullOrEmpty(nonce));

        var context = worker.ContextAs<AlephiumWorkerContext>();
        
        // dupe check
        if(!RegisterSubmit(nonce))
            throw new AlephiumStratumException(AlephiumStratumError.DuplicatedShare, $"duplicate share");

        return ProcessShareInternal(worker, nonce);
    }

    public void Init(AlephiumBlockTemplate blockTemplate)
    {
        Contract.RequiresNonNull(blockTemplate);
        
        JobId = blockTemplate.JobId;
        var target = new Target(blockTemplate.TargetBlob.HexToReverseByteArray().AsSpan().ToBigInteger());
        Difficulty = (double) new BigRational(AlephiumConstants.Diff1Target * 1024, target.ToBigInteger() * 1024) * AlephiumConstants.Pow2xDiff1TargetNumZero;
        blockTargetValue = target.ToUInt256();
        BlockTemplate = blockTemplate;

        jobParams = new AlephiumJobParams
        {
            JobId = JobId,
            FromGroup = BlockTemplate.FromGroup,
            ToGroup = BlockTemplate.ToGroup,
            HeaderBlob = BlockTemplate.HeaderBlob,
            TxsBlob = string.Empty,
            TargetBlob = string.Empty,
        };
    }
}