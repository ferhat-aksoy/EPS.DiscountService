using EPS.Discount.Core;

namespace EPS.Discount.Tests;

public class CodeGenerationTests
{
    private static readonly HashSet<char> AllowedAlphabet =
        "ABCDEFGHJKMNPQRSTUVWXYZ23456789".ToHashSet();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Generate_InvalidLength_Throws(int length)
    {
        var gen = new CodeGenerator();
        Assert.Throws<ArgumentOutOfRangeException>(() => gen.Generate(length));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(32)]
    public void Generate_ReturnsCorrectLength_AndAllowedChars(int length)
    {
        var gen = new CodeGenerator();
        var code = gen.Generate(length);

        Assert.Equal(length, code.Length);
        Assert.All(code, c => Assert.Contains(c, AllowedAlphabet));

        // Explicitly ensure ambiguous characters are not present
        Assert.DoesNotContain('0', code);
        Assert.DoesNotContain('1', code);
        Assert.DoesNotContain('O', code);
        Assert.DoesNotContain('I', code);
        Assert.DoesNotContain('L', code);
    }

    [Fact]
    public void Generate_ProducesDifferentValues()
    {
        var gen = new CodeGenerator();

        var codes = Enumerable.Range(0, 200)
                              .Select(_ => gen.Generate(12))
                              .ToArray();

        // Not all identical
        Assert.True(codes.Distinct(StringComparer.Ordinal).Count() > 1);

        // All chars in allowed alphabet
        Assert.All(codes.SelectMany(c => c), ch => Assert.Contains(ch, AllowedAlphabet));
    }

    [Theory]
    [InlineData(0, 8)]
    [InlineData(-10, 8)]
    public void GenerateBatch_InvalidCount_Throws(int count, int length)
    {
        var gen = new CodeGenerator();
        Assert.Throws<ArgumentOutOfRangeException>(() => gen.GenerateBatch(count, length).ToArray());
    }

    [Theory]
    [InlineData(10, 8)]
    [InlineData(100, 12)]
    public void GenerateBatch_UniqueAndCorrectSize(int count, int length)
    {
        var gen = new CodeGenerator();

        var batch = gen.GenerateBatch(count, length).ToArray();

        Assert.Equal(count, batch.Length);
        Assert.Equal(count, batch.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        Assert.All(batch, code =>
        {
            Assert.Equal(length, code.Length);
            Assert.All(code, c => Assert.Contains(c, AllowedAlphabet));
        });
    }

    [Fact]
    public async Task Generate_ThreadSafe_UnderConcurrency()
    {
        var gen = new CodeGenerator();

        int producers = Math.Max(4, Environment.ProcessorCount);
        int perProducer = 250;
        int length = 10;

        var tasks = Enumerable.Range(0, producers).Select(async _ =>
        {
            var local = new List<string>(perProducer);
            for (int i = 0; i < perProducer; i++)
            {
                local.Add(gen.Generate(length));
            }
            return local;
        });

        var results = await Task.WhenAll(tasks);
        var all = results.SelectMany(x => x).ToArray();

        // No exceptions, correct lengths and alphabet
        Assert.All(all, code =>
        {
            Assert.Equal(length, code.Length);
            Assert.All(code, c => Assert.Contains(c, AllowedAlphabet));
        });

        // Very high likelihood of uniqueness; allow tiny margin just in case
        int unique = all.Distinct(StringComparer.Ordinal).Count();
        Assert.True(unique >= all.Length - 2, $"Expected near-unique codes, got {unique}/{all.Length}");
    }
}
