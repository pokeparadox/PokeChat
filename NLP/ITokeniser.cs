namespace PokeChat.NLP;

public interface ITokeniser
{
    List<string> Tokenise(string input);
}
