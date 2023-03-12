using System.Globalization;
using System.Numerics;

namespace Miningcore.Blockchain.Evrmore;

public class EvrmoreConstants
{
    public const int EpochLength = 12000;
    public static readonly Org.BouncyCastle.Math.BigInteger Diff1B = new Org.BouncyCastle.Math.BigInteger("00ff000000000000000000000000000000000000000000000000000000", 16);
    public static readonly BigInteger Diff1 = BigInteger.Parse("00ff000000000000000000000000000000000000000000000000000000", NumberStyles.HexNumber);
    public const int TargetPaddingLength = 32;
    public const int ExtranoncePlaceHolderLength = 2;
}
