using System.Collections.Generic;

namespace PokeChatNet
{
    public partial class Columns
    {
        public const string PhraseTypeId = "PhraseTypeId";
        public const string SayWeighting = "SayWeighting";
        public const string SeeWeighting = "SeeWeighting";
    }

    public class Phrase : DataIndex
    {
        public override void SetData(Dictionary<string, string> r)
        {
            base.SetData(r);
            PhraseTypeId = r[Columns.PhraseTypeId].ToInt();
            SayWeighting = r[Columns.SayWeighting].ToFloat();
            SeeWeighting = r[Columns.SeeWeighting].ToFloat();
        }

        public int PhraseTypeId { get; set;}
        public float SayWeighting { get; set;}
        public float SeeWeighting { get; set;}
    }
}

