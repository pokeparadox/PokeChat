namespace PokeChat.NLP;

public interface ISentenceSplitter
{
    List<string> Split(string input);
}
