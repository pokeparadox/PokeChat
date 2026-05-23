namespace PokeChat.Maths;

public record MathResult(string Expression, double Value, double? StatedResult);

public interface IMathEngine
{
    MathResult? Evaluate(string input);
}
