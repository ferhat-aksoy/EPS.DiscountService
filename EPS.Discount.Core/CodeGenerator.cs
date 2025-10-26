using System.Buffers;
using System.Security.Cryptography;
using EPS.Discount.Core.Interfaces;

namespace EPS.Discount.Core;

public sealed class CodeGenerator: ICodeGenerator
{
    private static readonly char[] Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789".ToCharArray();
    private static readonly int AlphabetLen = Alphabet.Length;

    public string Generate(int length)
    {
        if (length < 1)
            throw new ArgumentOutOfRangeException(nameof(length));

        var bytePool = ArrayPool<byte>.Shared;
        var charPool = ArrayPool<char>.Shared;

        byte[] bytes = bytePool.Rent(length);
        char[] chars = charPool.Rent(length);

        try
        {
            RandomNumberGenerator.Fill(bytes);

            for (int i = 0; i < length; i++)
            {
                chars[i] = Alphabet[bytes[i] % AlphabetLen];
            }

            return new string(chars, 0, length);
        }
        finally
        {
            bytePool.Return(bytes);
            charPool.Return(chars);
        }
    }

    public IEnumerable<string> GenerateBatch(int count, int length)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count));

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (result.Count < count)
        {
            result.Add(Generate(length));
        }
        return result;
    }
}