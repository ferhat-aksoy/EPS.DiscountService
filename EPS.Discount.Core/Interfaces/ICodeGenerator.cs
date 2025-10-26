namespace EPS.Discount.Core.Interfaces;

public interface ICodeGenerator
{
    string Generate(int length);
    IEnumerable<string> GenerateBatch(int count, int length);
}
