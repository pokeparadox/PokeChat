using System.Collections.Generic;

namespace PokeChatNet
{
    public partial class Columns
    {
        public const string PronounWordId = "PronounWordId";
        public const string VerbWordTypeId = "VerbWordTypeId";
    }

    public class VerbLookup : DataIndex
    {
        public override void SetData(Dictionary<string, string> r)
        {
            base.SetData(r);
            PronounWordId = r[Columns.PronounWordId].ToInt();
            VerbWordTypeId = r[Columns.VerbWordTypeId].ToInt();
        }

        public int PronounWordId { get; set;}
        public int VerbWordTypeId { get; set;}
    }
}

