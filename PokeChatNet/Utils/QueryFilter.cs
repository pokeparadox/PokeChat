using System.Collections.Generic;
using System.Linq;

namespace PokeChatNet
{
    public class QueryFilter 
    {
        public QueryFilter()
        {
        }

        public QueryFilter(string column, int val)
        {
            Add(column,val.ToString());
        }

        public QueryFilter(string column, float val)
        {
            Add(column,val.ToString());
        }

        public QueryFilter(string column, string val)
        {
            Add(column,val);
        }

        public QueryFilter(string column, List<int> ids)
        {
            Add(column, ids);
        }

        public enum QueryMode
        {
            Or,
            And
        };

        public void Add<T>(string column, T value)
        {
            _parameters[column] = value.ToString();
        }

        public void Add(string column, List<int> ids)
        {
            _parameters[column] = column;
            lookupIds = ids;
            Mode = QueryMode.Or;
        }

        public void Add(string column, string value)
        {
            _parameters[column] = value;
        }

        public string[] Columns()
        {
            List<string> output = new List<string>();
            foreach (var k in _parameters.Keys)
            {
                output.Add(k);
            }
            return output.ToArray();
        }

        public string[] Values()
        {
            List<string> output = new List<string>();
            foreach (var k in _parameters.Keys)
            {
                output.Add(_quote +_parameters[k] + _quote);
            }
            return output.ToArray();
        }

        public override string ToString()
        {
            string output = string.Empty;

            string m; 
            if (Mode == QueryMode.And)
            {
                m = _and;
            }
            else if (Mode == QueryMode.Or)
            {
                m = _or;
            }
            else
            {
                m = string.Empty;
            }
            if (lookupIds != null)
            {
                for (int i = 0; i < lookupIds.Count; ++i)
                {
                    output += string.Join(_space,_space,_parameters.Keys.First(), _equals, _quote + i.ToString() + _quote, m);
                }
            }
            else
            {
                foreach (var k in _parameters.Keys)
                {
                    output += string.Join(_space, _space,k, _equals, _quote +_parameters[k]+_quote, m);
                }
            }


            return output.TrimEnd(m);
        }

        Dictionary<string,string> _parameters = new Dictionary<string, string>();
        List<int> lookupIds;
        public QueryMode Mode = QueryMode.And;
        const string _and = "AND";
        const string _or = "OR";
        const string _equals = "=";
        const string _space = " ";
        const string _quote = "'";
    }
}

