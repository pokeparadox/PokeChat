using System.Collections.Generic;

namespace PokeChatNet
{
    public partial class Columns
    {
         public const string WordTypeId = "WordTypeId";
    }

    public class WordTypeWord : DataIndex
    {
        public override void SetData(Dictionary<string, string> r)
        {
            base.SetData(r);
            WordId = r[Columns.WordId].ToInt();
            WordTypeId = r[Columns.WordTypeId].ToInt();
        }

        public int WordId { get; set;}
        public int WordTypeId { get; set;}
    }
}

