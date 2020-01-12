using System.Collections.Generic;

namespace PokeChatNet
{
    public static class VerbLookupQueries
    {
        const string Table = "VerbLookup";
        public static bool Exists(int id)
        {
            return Queries.Exists(Table, id);
        }

        public static bool Exists(int pronounId, int verbId)
        {
            var f = new QueryFilter(Columns.PronounWordId, pronounId);
            f.Add(Columns.VerbWordTypeId, verbId);
            return Queries.Exists(Table, f);
        }

        public static List<VerbLookup> Select()
        {
            return Queries.SelectTable<VerbLookup>(Table);
        }

        public static VerbLookup Select(int id)
        {
            return Queries.SelectRow<VerbLookup>(Table, id);
        }

        public static VerbLookup Select(int pronounWordId, int verbWordTypeId)
        {
            var f = new QueryFilter(Columns.PronounWordId, pronounWordId);
            f.Add(Columns.VerbWordTypeId, verbWordTypeId);
            return Queries.Select(Table, f).ToData<VerbLookup>();
        }

        public static int Insert(int pronounWordId, int verbWordTypeId )
        {
            var f = new QueryFilter(Columns.PronounWordId, pronounWordId);
            f.Add(Columns.VerbWordTypeId, verbWordTypeId);
            return Queries.Insert(Table, f);
        }

        public static VerbLookup SelectOrInsert(Word pronounWord, WordType verbWordType)
        {
            return SelectOrInsert(pronounWord.Id, verbWordType.Id);
        }

        public static VerbLookup SelectOrInsert(int pronounWordId, int verbWordTypeId)
        {
            if (Exists(pronounWordId, verbWordTypeId))
            {
                return Select(pronounWordId, verbWordTypeId);
            }
            int i = Insert(pronounWordId, verbWordTypeId);
            return Select(i);
        }
    }
}

