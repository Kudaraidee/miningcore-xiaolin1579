using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using Miningcore.Util;

// ReSharper disable InconsistentNaming

namespace Miningcore.Blockchain.Alephium;

public static class AlephiumConstants
{
    public const int Diff1TargetNumZero = 30;
    public static BigInteger Diff1Target = BigInteger.Pow(2, 256 - Diff1TargetNumZero) - 1;
    public static readonly double Pow2xDiff1TargetNumZero = Math.Pow(2, Diff1TargetNumZero);
    public static int GroupSize = 4;
    public static int NonceLength = 24;
    public static int NumZeroAtLeastInHash = 37;
    public static int HashLength = 32;
    public const uint ShareMultiplier = 1;
    // ALPH smallest unit is called PHI: https://wiki.alephium.org/glossary#gas-price
    public const decimal SmallestUnit = 1000000000000000000;
    
    // Socket miner API
    public const int MessageHeaderSize = 4; // 4 bytes body length
    public const byte JobsMessageType = 0x00;
    public const byte SubmitResultMessageType = 0x01;
    public const byte SubmitBlockMessageType = 0x00;

    // Gas (Transaction)
    public const decimal UtxoConsolidationNodeReleaseVersion = 230;
    public const decimal DefaultGasPrice = 100000000000;
    public const int TxBaseGas = 1000;
    public const int MinGasPerTx = 20000;
    public const int MaxGasPerTx = 625000;
    public const int GasPerInput = 2000;
    public const int GasPerOutput = 4500;
    public const int P2pkUnlockGas = 2060;
}