using System;
using System.Collections.Generic;

namespace PokeChatNet
{
    public static class VerbLookups
    {
        public static VerbLookup I = VerbLookupQueries.SelectOrInsert(Pronouns.I, WordTypes.VerbI);
        public static VerbLookup You = VerbLookupQueries.SelectOrInsert(Pronouns.You, WordTypes.VerbYou);
        public static VerbLookup He = VerbLookupQueries.SelectOrInsert(Pronouns.He, WordTypes.VerbHe);
        public static VerbLookup She = VerbLookupQueries.SelectOrInsert(Pronouns.She, WordTypes.VerbShe);
        public static VerbLookup It = VerbLookupQueries.SelectOrInsert(Pronouns.It, WordTypes.VerbIt);
        public static VerbLookup We = VerbLookupQueries.SelectOrInsert(Pronouns.We, WordTypes.VerbWe);
        public static List<VerbLookup> All
        {
            get
            {
                return new List<VerbLookup> { I,You,He,She,It,We};
            }
        }
    }
}

