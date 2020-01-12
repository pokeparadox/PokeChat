using System.Collections.Generic;

namespace PokeChatNet
{
    public partial class Columns
    {
        public const string AltWordId = "AltWordId";
    }

	public class Synonym : DataIndex
	{
        public override void SetData(Dictionary<string, string> r)
        {
            base.SetData(r);
            WordId = r[Columns.WordId].ToInt();
            AltWordId = r[Columns.AltWordId].ToInt();
        }

        public int WordId { get; set;}
        public int AltWordId { get; set;}
	}
}

