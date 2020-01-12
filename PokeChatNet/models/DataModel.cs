using System.Collections.Generic;
namespace PokeChatNet
{
    public partial class  Columns
    {
        public const string Name = "Name";
    }
	public class DataModel : DataIndex
	{
        public override void SetData(Dictionary<string, string> r)
        {
            base.SetData(r);
            Name = r[Columns.Name];
        }

		public string Name { get; set;}
	}
}

