using System.Collections.Generic;

namespace PokeChatNet
{
    public partial class Columns
    {
        public const string PhraseId = "PhraseId";
        public const string Position = "Position";
    }

	public class PhraseWord : DataIndex
	{
        public override void SetData(Dictionary<string, string> r)
        {
            base.SetData(r);
            PhraseId = r[Columns.PhraseId].ToInt();
            WordId = r[Columns.WordId].ToInt();
            Position = r[Columns.Position].ToInt();
        }

		public int PhraseId { get; set;}
		public int WordId { get; set;}
        public int Position { get; set;}
	}
}

