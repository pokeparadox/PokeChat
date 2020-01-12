using System.Collections.Generic;
namespace PokeChatNet
{
    public partial class  Columns
    {
        public const string Id = "Id";
    }

	public class DataIndex
	{
        public DataIndex()
        {
        }

        public virtual void SetData(Dictionary<string,string> r)
        {
            Id = r[Columns.Id].ToInt();
        }

		public int Id { get; set;}
	}
}

