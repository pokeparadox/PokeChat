using System;
using System.Collections.Generic;

namespace PokeChatNet
{
	public class Word : DataModel
	{
        public override void SetData(Dictionary<string, string> r)
        {
            base.SetData(r);
            SayWeighting = r[Columns.SayWeighting].ToFloat();
            SeeWeighting = r[Columns.SeeWeighting].ToFloat();
        }

		//public int WordTypeId { get; set;}
		public float SayWeighting { get; set;}
		public float SeeWeighting { get; set;}
	}
}

