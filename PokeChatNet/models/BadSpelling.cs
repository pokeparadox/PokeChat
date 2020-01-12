using System.Collections.Generic;

namespace PokeChatNet
{
    public partial class  Columns
    {
        public const string WordId = "WordId";
    }

	public class BadSpelling : DataModel
	{
        public override void SetData(Dictionary<string, string> r)
        {
            base.SetData(r);
            WordId = r[Columns.WordId].ToInt();
        }

		public int WordId { get; set;}
	}
}

