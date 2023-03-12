using Miningcore.Extensions;
using Org.BouncyCastle.Math;

namespace Miningcore.Blockchain.Evrmore;

public static class EvrmoreUtils
{
    public static string EncodeTarget(double difficulty)
    {
        string result;
        var diff = BigInteger.ValueOf((long) (difficulty * 255d));
        var quotient = EvrmoreConstants.Diff1B.Divide(diff).Multiply(BigInteger.ValueOf(255));
        var bytes = quotient.ToByteArray().AsSpan();
        Span<byte> padded = stackalloc byte[EvrmoreConstants.TargetPaddingLength];

        var padLength = EvrmoreConstants.TargetPaddingLength - bytes.Length;

        if(padLength > 0)
        {
            bytes.CopyTo(padded.Slice(padLength, bytes.Length));
            result = padded.ToHexString(0, EvrmoreConstants.TargetPaddingLength);
        }

        else
            result = bytes.ToHexString(0, EvrmoreConstants.TargetPaddingLength);

        return result;
    }
}