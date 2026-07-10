namespace PokeChat.NLP;

public interface IPosTagger
{
    List<PosTag> Tag(List<string> tokens);
    bool IsKnownWord(string word);
}