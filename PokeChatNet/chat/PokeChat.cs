using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PokeChatNet
{
    public class PokeChat
    {
        private static class PhrasePatterns
        {
            public static void Init()
            {
                var t = Action;
                t = Describe;
            }
            // I am reading the book
            // Pronoun verbI PresentPart. determiner noun
            // 
            // my car is fast
            // determiner noun verbIt adjective  
            public static readonly List<PhrasePattern> Action = 
                PhrasePatternQueries.SelectOrInsert(Patterns.Action, PhraseTypes.Doing, 
                    new List<WordType> {WordTypes.Pronoun, WordTypes.Verb, WordTypes.PresentParticiple, WordTypes.Determiner, WordTypes.Noun});                                                                                         
            public static readonly List<PhrasePattern> Describe = 
                PhrasePatternQueries.SelectOrInsert(Patterns.Describe, PhraseTypes.Description, 
                                                    new List<WordType> {WordTypes.Determiner, WordTypes.Noun, WordTypes.Verb, WordTypes.Adjective});

        }

        bool gaveGreeting = false;
        string lastRawInput;
        string lastRawOutput;
        public PokeChat()
        {
            SetupData();
        }

        void SetupData()
        {
            SqliteAccess.Db.FileName = "data/pokechat.db";
            SetupGreetings();
            SetupQuestions();
            SetupNouns();
            SetupAdjectives();

            SetupDeterminers();
            SetupPronouns();
            SetupVerbs();
            SetupVerbI();
            SetupVerbYou();
                      
            // TODO then insert etc...
            WordTypeQueries.SelectOrInsert("interjection");     // oh!, Right! ouch!.
            WordTypeQueries.SelectOrInsert("conjunction");      // and, or, but, with, if, when
            WordTypeQueries.SelectOrInsert("preposition");      // to, on, of, under, over, for, in, accross
            WordTypeQueries.SelectOrInsert("adverb");           // describes the verb (drive quickly)
                    
            // Special nouns
            WordTypeQueries.SelectOrInsert("name");
            WordTypeQueries.SelectOrInsert("place");
            PhrasePatterns.Init();
        }

        void SetupVerbs()
        {
            var wt = WordTypes.Verb;
            WordQueries.SelectOrInsert("be", wt.Id);
            WordQueries.SelectOrInsert("have", wt.Id);
            WordQueries.SelectOrInsert("make", wt.Id);
            WordQueries.SelectOrInsert("want", wt.Id);
            WordQueries.SelectOrInsert("eat", wt.Id);
            WordQueries.SelectOrInsert("have", wt.Id);
            WordQueries.SelectOrInsert("like", wt.Id);
            WordQueries.SelectOrInsert("think", wt.Id);
            WordQueries.SelectOrInsert("win", wt.Id);
            WordQueries.SelectOrInsert("wish", wt.Id);
            WordQueries.SelectOrInsert("do", wt.Id);
        }

        void SetupVerbI()
        {
            var wt = WordTypes.VerbI;
            WordQueries.SelectOrInsert("am", wt.Id);
            WordQueries.SelectOrInsert("have", wt.Id);
            WordQueries.SelectOrInsert("eat", wt.Id);
            WordQueries.SelectOrInsert("like", wt.Id);
            WordQueries.SelectOrInsert("want", wt.Id);
            WordQueries.SelectOrInsert("think", wt.Id);
            WordQueries.SelectOrInsert("win", wt.Id);
            WordQueries.SelectOrInsert("wish", wt.Id);
            WordQueries.SelectOrInsert("can", wt.Id);
            WordQueries.SelectOrInsert("do", wt.Id);
        }

        void SetupVerbYou()
        {
            var wt = WordTypes.VerbYou;
            WordQueries.SelectOrInsert("are", wt.Id);
            WordQueries.SelectOrInsert("have", wt.Id);
            WordQueries.SelectOrInsert("eat", wt.Id);
            WordQueries.SelectOrInsert("like", wt.Id);
            WordQueries.SelectOrInsert("want", wt.Id);
            WordQueries.SelectOrInsert("think", wt.Id);
            WordQueries.SelectOrInsert("win", wt.Id);
            WordQueries.SelectOrInsert("wish", wt.Id);
            WordQueries.SelectOrInsert("can", wt.Id);
            WordQueries.SelectOrInsert("do", wt.Id);
        }

        void SetupGreetings()
        {
            var wt = WordTypes.Greeting;
            WordQueries.SelectOrInsert("hello", wt.Id);
            WordQueries.SelectOrInsert("hi", wt.Id);
            WordQueries.SelectOrInsert("yo", wt.Id);
            WordQueries.SelectOrInsert("wassup", wt.Id);
            WordQueries.SelectOrInsert("eyup", wt.Id);
            WordQueries.SelectOrInsert("hey", wt.Id);
            WordQueries.SelectOrInsert("welcome", wt.Id);
            WordQueries.SelectOrInsert("greetings", wt.Id);
        }

        string SelectGreeting(string text)
        {
            var wt = WordTypes.Greeting;
            WordQueries.SelectOrInsert(text, wt.Id);
            var words = WordQueries.SelectByWordType(wt.Name);
            var r = new Random();
            int index = r.Next(0, words.Count-1);
            var g = words[index];
            return g.Name;
        }

        void SetupDeterminers()
        {
            var wt = WordTypes.Determiner; // a, an, the, my, this, that
            WordQueries.SelectOrInsert("a", wt.Id);
            WordQueries.SelectOrInsert("an", wt.Id);
            WordQueries.SelectOrInsert("the", wt.Id);
            WordQueries.SelectOrInsert("my", wt.Id);
            WordQueries.SelectOrInsert("this", wt.Id);
            WordQueries.SelectOrInsert("that", wt.Id);
        }

        void SetupAdjectives()
        {
            // Describes noun
            var t = WordTypeQueries.SelectOrInsert("adjectives");
            WordQueries.SelectOrInsert("fast", t.Id);
            WordQueries.SelectOrInsert("big", t.Id);
            WordQueries.SelectOrInsert("small", t.Id);
            WordQueries.SelectOrInsert("long", t.Id);
            WordQueries.SelectOrInsert("short", t.Id);
            WordQueries.SelectOrInsert("cool", t.Id);
            WordQueries.SelectOrInsert("hot", t.Id);
            WordQueries.SelectOrInsert("expensive", t.Id);
        }

        void SetupNouns()
        {
            var t = WordTypeQueries.SelectOrInsert("noun");
            WordQueries.SelectOrInsert("book", t.Id);
            WordQueries.SelectOrInsert("table", t.Id);
            WordQueries.SelectOrInsert("mat", t.Id);
            WordQueries.SelectOrInsert("door", t.Id);
            WordQueries.SelectOrInsert("floor", t.Id);
            WordQueries.SelectOrInsert("gate", t.Id);
            WordQueries.SelectOrInsert("movie", t.Id);
            WordQueries.SelectOrInsert("song", t.Id);
        }

        void SetupPronouns()
        {
            var wt = WordTypes.Pronoun;
            WordQueries.SelectOrInsert("i", wt.Id);
            WordQueries.SelectOrInsert("you", wt.Id);
            WordQueries.SelectOrInsert("he", wt.Id);
            WordQueries.SelectOrInsert("she", wt.Id);
            WordQueries.SelectOrInsert("it", wt.Id);
            WordQueries.SelectOrInsert("we", wt.Id);
            WordQueries.SelectOrInsert("they", wt.Id);
        }

        void SetupQuestions()
        {
            var wt = WordTypes.Question;
            WordQueries.SelectOrInsert("how", wt.Id);
            WordQueries.SelectOrInsert("who", wt.Id);
            WordQueries.SelectOrInsert("what", wt.Id);
            WordQueries.SelectOrInsert("why", wt.Id);
            WordQueries.SelectOrInsert("when", wt.Id);
            WordQueries.SelectOrInsert("where", wt.Id);
            WordQueries.SelectOrInsert("which", wt.Id);
        }



        public void SystemMessage(string message)
        {
            // Deconstruct message
            // Insert words
            // Insert Phrase
            // Display to console

            Message("Sys", message);
        }

        public void Message(string name, string message)
        {
            lastRawOutput = message;
            Console.WriteLine(name + ": " + message);
        }

        public void Chat()
        {
            SystemMessage("Welcome to PokeChat.");
            SystemMessage("Type '@exit' to Quit.");
            SystemMessage("Type a greeting to begin...");
            while (CommandProcessor.Running)
            {
                string userText = Console.ReadLine();
                lastRawInput = userText;
                if (!CommandProcessor.Process(userText))
                {
                    var reply = ProcessReply(userText);
                    Message("BOT", reply);
                }
            }
        }

        public string CreatePhrase(Pattern pattern, Word pronoun, Word noun)
        {
            var patternParts = PhrasePatternQueries.SelectByPattern(pattern.Id);
            var r = new Random();
            var b = new StringBuilder();
            foreach (var p in patternParts)
            {
                if (p.WordTypeId == WordTypes.Pronoun.Id && WordTypeWordQueries.Exists(WordTypes.Pronoun, pronoun))
                {
                    b.Append(pronoun.Name);
                }
                else if (p.WordTypeId == WordTypes.Noun.Id)
                {
                    b.Append(noun.Name);
                    WordQueries.SelectOrInsert(noun.Name, WordTypes.Noun);
                }
                else if (p.WordTypeId == WordTypes.Verb.Id)
                {
                    var verbTypeLookup = VerbLookups.All.FirstOrDefault(v => v.PronounWordId == pronoun.Id);
                    int verbTypeId = verbTypeLookup.VerbWordTypeId;
                    var wt = WordTypeQueries.Select(verbTypeId);
                    var words = WordTypeWordQueries.SelectByWordType(wt.Id);
                    if(words != null && words.Any())
                    {
                        int i = r.Next(0, words.Count - 1);
                        var w = WordQueries.Select(words[i].WordId);
                        b.Append(w.Name);
                    }
                }
                else
                {
                    var words = WordTypeWordQueries.SelectByWordType(p.WordTypeId);
                    if(words != null && words.Any())
                    {
                        int i = r.Next(0, words.Count - 1);
                        var w = WordQueries.Select(words[i].WordId);
                        b.Append(w.Name);
                    }
                }
                b.Append(" ");
            }
            return b.ToString();
        }

        public bool IsQuestion(string text, out string questionWord)
        {
            var questionWords = WordQueries.SelectByWordType(WordTypes.Question.Name).ConvertAll(x => x.Name);
            questionWord = questionWords.FirstOrDefault(x => text.Contains(x));
            return !string.IsNullOrEmpty(questionWord);
        }

        string AnswerQuestion(string text)
        {
            // 
            return text;
        }

        string ProcessReply(string text)
        {
            text = text.ToLowerInvariant();
            string questionWord;
            if (IsQuestion(text, out questionWord))
            {
                text = AnswerQuestion(text);
            }
            else if (!gaveGreeting)
            {
                text = SelectGreeting(text);
                gaveGreeting = true;
            }
            else
            {
                text = CreatePhrase(Patterns.Action, Pronouns.I, WordQueries.SelectOrInsert("book"));
            }

            return text;
        }
    }
}

