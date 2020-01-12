namespace PokeChatNet
{
    public partial class Columns
    {
        public const string PatternId = "PatternId";
    }

    public class PhrasePattern : DataIndex
    {
       
        public override void SetData(System.Collections.Generic.Dictionary<string, string> r)
        {
            base.SetData(r);
            WordTypeId = r[Columns.WordTypeId].ToInt();
            PatternId = r[Columns.PatternId].ToInt();
            PhraseTypeId = r[Columns.PhraseTypeId].ToInt();
            Position = r[Columns.Position].ToInt();
        }

        public int Position { get; set;}
        public int WordTypeId { get; set;}
        public int PhraseTypeId { get; set;}
        public int PatternId { get; set;}
    }
}

