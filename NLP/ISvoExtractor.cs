namespace PokeChat.NLP;

public interface ISvoExtractor
{
    List<SvoTriple> Extract(List<string> tokens, List<PosTag> tags);
}
