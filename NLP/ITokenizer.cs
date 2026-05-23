namespace PokeChat.NLP;

public interface ITokenizer
{
    List<string> Tokenize(string input);
}
