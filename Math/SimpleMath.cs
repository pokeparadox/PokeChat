using System.Text.RegularExpressions;

namespace PokeChat.Math;

public class SimpleMath : IMathEngine
{
    private static readonly Regex MathExprRegex = new(
        @"(\d+(?:\.\d+)?)\s*([+\-*/^])\s*(\d+(?:\.\d+)?)",
        RegexOptions.Compiled);

    private static readonly Regex EqualsRegex = new(
        @"\s*=\s*(\d+(?:\.\d+)?)\s*$",
        RegexOptions.Compiled);

    public MathResult? Evaluate(string input)
    {
        var cleaned = input
            .ToLowerInvariant()
            .Replace("plus", "+")
            .Replace("minus", "-")
            .Replace("times", "*")
            .Replace("multiplied by", "*")
            .Replace("divided by", "/")
            .Replace("to the power of", "^");

        var match = MathExprRegex.Match(cleaned);
        if (!match.Success)
            return null;

        var expr = match.Value;
        var eqMatch = EqualsRegex.Match(cleaned, match.Index + match.Length);

        double? statedResult = null;
        if (eqMatch.Success && double.TryParse(eqMatch.Groups[1].Value, out var stated))
            statedResult = stated;

        var value = EvaluateBinary(expr);
        if (double.IsNaN(value) || double.IsInfinity(value))
            return null;

        return new MathResult(expr, value, statedResult);
    }

    private static double EvaluateBinary(string expr)
    {
        var match = MathExprRegex.Match(expr);
        if (!match.Success)
            return double.NaN;

        if (!double.TryParse(match.Groups[1].Value, out var left))
            return double.NaN;

        if (!double.TryParse(match.Groups[3].Value, out var right))
            return double.NaN;

        var op = match.Groups[2].Value;

        return op switch
        {
            "+" => left + right,
            "-" => left - right,
            "*" => left * right,
            "/" => right != 0 ? left / right : double.NaN,
            "^" => System.Math.Pow(left, right),
            _ => double.NaN
        };
    }
}
