# PokeChat — Agent Notes

## Project
- C# console app, .NET 10 (`net10.0`)
- Single project: `PokeChat.csproj` (solution: `PokeChat.slnx`)
- SQLite via **`Microsoft.EntityFrameworkCore.Sqlite`** (EF Core, not raw `Microsoft.Data.Sqlite`)
- **`Facet`** + **`Facet.Extensions.EFCore`** for entity-to-model mapping (`[Facet(typeof(FactEntity))]` partial class)
- Dependencies: `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Design`, `Facet`, `Facet.Extensions.EFCore`

## Commands
```bash
dotnet build          # build
dotnet run            # run the chat application
dotnet test           # run all tests
```

## Architecture
Terminal chat bot with custom NLP parser (no LLMs). Learns facts from conversations and stores them in SQLite via EF Core. All conversational data (greetings, response rules, POS dictionary, name patterns, bot commands, bot responses) is stored in DB — the bot learns and grows its vocabulary over time.

```
Program.cs                    → entry point, creates ChatSession
Core/
  ChatSession.cs              → main loop: greet → parse → respond → store
  GreetingPool.cs             → loads random greeting from DB via KnowledgeStore
  ContextKeys.cs              → constants for context tracker keys
  PredicateType.cs            → enum for predicate classification
NLP/
  Tokeniser.cs                → British English spelling, whitespace + punctuation tokenisation (implements ITokeniser)
  PosTagger.cs                → DB-loaded dictionary (pos_dictionary table) + heuristics (implements IPosTagger)
  SvoExtractor.cs             → Subject-Verb-Object triple extraction (implements ISvoExtractor)
  SentenceSplitter.cs         → multi-sentence splitting on `.`, `!`, `?` (implements ISentenceSplitter)
  PunctuationHelper.cs        → shared IsPunctuation utility
  SpellChecker.cs             → Levenshtein-based spell correction with misspellings table
  Interfaces (IPosTagger, ITokeniser, ISentenceSplitter, ISvoExtractor)
Math/
  IMathEngine.cs              → interface for math expression evaluation
  SimpleMath.cs               → regex-based binary expression engine (+, -, *, /, ^)
Knowledge/
  KnowledgeStore.cs           → EF Core repository layer over PokeChatDbContext
  Fact.cs                     → Facet model mapping to FactEntity
  ContextTracker.cs           → conversation context, pronoun resolution
Responses/
  ResponseEngine.cs           → rule-based response generation (math, dictionary/thesaurus, rules, facts, follow-ups)
  ResponseRules.cs            → loads rules from DB (response_rules table), regex matching
Data/
  PokeChatDbContext.cs        → EF Core DbContext with DbSets for all entities
  DbSeeder.cs                 → seeds initial data (greetings, rules, POS dictionary, bot responses, etc.)
  pos_dictionary.json         → ~2850 POS entries (incl. British English variants) loaded by DbSeeder at seed time
  Schema.sql                  → all tables
  Entities/                   → entity classes: User, FactEntity, Conversation, Greeting, GreetingWord,
                                ResponseRule, ResponseRuleResponse, PosDictionaryEntry, NamePattern,
                                BotCommand, Misspelling, BotResponse, WordDefinition, WordLink
```

## Key Details
- **DB location:** `pokechat.db` in project root (resolved by walking up from `BaseDirectory` to find `PokeChat.csproj`)
- **DB init:** `Database.EnsureCreated()` in `PokeChatDbContext` constructor (no embedded resource or file walking needed for schema)
- **Seeder:** `DbSeeder.Seed()` populates greetings, greeting words, response rules, POS dictionary (from `pos_dictionary.json`), name patterns, bot commands, misspellings, and bot responses on first run
- **Knowledge extraction:** "my name is Alice" → (user, is_named, Alice); "I like pizza" → (user, likes, pizza); "the sky is blue" → (sky, is, blue) [general knowledge]
- **Pronoun resolution:** ContextTracker resolves "it/this/that" → last object, "he/she/they" → last subject; "him/her/them" → last object
- **Response flow:** unknown word check → math evaluation → dictionary/thesaurus query → link creation → pattern match from DB rules → check existing facts → context follow-up → random user fact → DB-loaded default responses
- **PosTagger:** Instance-based (implements `IPosTagger`), initialized from `pos_dictionary` table; no hardcoded dictionary in code
- **Response rules:** Loaded from `response_rules` + `response_rule_responses` tables (regex patterns with responses)
- **Bot responses:** ResponseEngine templates (defaults, follow-ups, clarification prompts) stored in `bot_responses` table, loaded at construction time
- **Greeting learning:** When user responds to name prompt with a novel first word, it's learned as a greeting word
- **Name extraction:** Uses `name_patterns` table (e.g. "my name is", "i am", "call me") to extract names from input
- **Bot commands:** Exit commands loaded from `bot_commands` table (`quit`, `exit`, etc.)
- **ChatSession:** Implements `IDisposable` to clean up the DbContext
- **NLP interfaces:** All NLP components implement interfaces (`ITokeniser`, `IPosTagger`, `ISentenceSplitter`, `ISvoExtractor`) for testability
- **SpellChecker:** Levenshtein-based spell correction with `misspellings` table for known errors; `pos_dictionary` as known word dictionary
- **KnowledgeStore.Save():** Batch save method replaces per-operation SaveChanges; callers call `Save()` at logical boundaries

## DB Schema
- `users` — id, name (unique), first_seen, last_seen
- `facts` — id, user_id (nullable FK→users), subject, verb, object, predicate_type, created_at
- `conversations` — id, user_id (nullable FK→users), user_input, bot_response, timestamp
- `greetings` — id, text, is_system, created_at
- `greeting_words` — id, word (unique), learned_from_user_id (nullable FK→users), created_at
- `response_rules` — id, pattern, input_type, is_active, created_at
- `response_rule_responses` — id, rule_id (FK→response_rules, CASCADE), response_text
- `pos_dictionary` — id, word, word_type, created_at
- `name_patterns` — id, pattern, created_at
- `bot_commands` — id, command (unique), created_at
- `misspellings` — id, wrong_word (unique), correction, created_at
- `bot_responses` — id, category, response_text, created_at
- `word_definitions` — id, word, definition, defined_by_user_id (nullable FK→users), created_at
- `word_links` — id, source_word, target_word, link_type, created_by_user_id (nullable FK→users), created_at

## Improvement Plan
A phased improvement plan is maintained in `.agents/plan.md`, ordered by priority:
- **Phase 1:** Critical bug fixes ✅ (GetFact client-side filtering, proper noun dead code, abbreviation detection, pronoun resolution, empty bot responses)
- **Phase 2:** High priority ✅ (batch SaveChanges, PosTagger static state, schema-entity mismatch, duplicate POS entries, predicate enum, context key constants)
- **Phase 3:** Medium priority ✅ (tag duplicate handling, IsPunctuation dedup, test helper consolidation, NLP interfaces, test coverage, POS data file extraction, ResponseEngine strings to DB)
- **Phase 4:** Low priority (using var, Random consolidation, lazy EnsureCreated, date format evaluation, DbPath robustness)
- **Phase 5:** British English ✅ (tokeniser renaming, 91 British word variants in pos_dictionary.json)
- **Phase 6:** Simple Mathematics ✅ (IMathEngine/SimpleMath with +,-,*,/,^, regex-based, stated-result correction)
- **Phase 7:** Self-Learning Dictionary ✅ (WordDefinition/WordLink entities, definition query/learn, thesaurus, link creation)

## Known Fixes
- **Math operators in tokeniser:** `+`, `-`, `*`, `/`, `^` are extracted as standalone tokens by Tokeniser regex. `GetUnknownWords` in `SpellChecker` must skip math operators to prevent false unknown-word prompts before math evaluation. Fixed via `SpellChecker.MathOperators` HashSet.
- **Solution file path:** `PokeChat.slnx` must use `tests/PokeChat.Tests/PokeChat.Tests.csproj` (not `../tests/...`) — the `..` resolved to a stale project copy at `/mnt/Storage/RiderProjects/tests/`.
- **Re-seeding after new categories:** `SeedBotResponses` and all other `Seed*` methods check `if (context.X.Any()) return;`. When new categories or responses are added to the seeder, existing `pokechat.db` must be deleted to get the new seed data.

## Git
- `.gitignore` excludes `/bin`, `/obj`, `/graphify-out`
- `pokechat.db` IS gitignored now
- `mempalace.yaml` and `entities.json` are gitignored
