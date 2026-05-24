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
  Pluraliser.cs               → singularise English plural nouns
  Interfaces (IPosTagger, ITokeniser, ISentenceSplitter, ISvoExtractor)
Math/
  IMathEngine.cs              → interface for math expression evaluation
  SimpleMath.cs               → regex-based binary expression engine (+, -, *, /, ^)
  Core/
    INounCategoriser.cs         → interface for noun categorisation
    NounCategoriser.cs          → DB lookup + heuristics (person/place/thing), auto-learns
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
                                  BotCommand, Misspelling, BotResponse, WordDefinition, WordLink, NounCategory
```

## Key Details
- **DB location:** `pokechat.db` in project root (resolved by walking up from `BaseDirectory` to find `PokeChat.csproj`); override via `POKECHAT_DB_PATH` environment variable
- **DB init:** `Database.EnsureCreated()` called lazily in `ChatSession()` constructor (not in `PokeChatDbContext` constructor)
- **Seeder:** `DbSeeder.Seed()` populates greetings, greeting words, response rules, POS dictionary (from `pos_dictionary.json`), name patterns, bot commands, misspellings, and bot responses on first run
- **Knowledge extraction:** "my name is Alice" → (user, is_named, Alice); "I like pizza" → (user, likes, pizza); "the sky is blue" → (sky, is, blue) [general knowledge]
- **Pronoun resolution:** ContextTracker resolves "it/this/that" → last object, "he/she/they" → last subject; "him/her/them" → last object
- **Response flow:** unknown word check → math evaluation → dictionary/thesaurus query → link creation → pattern match from DB rules → check existing facts (verb conjugated via ConjugateVerb) → context follow-up → random user fact → proactive question from user facts (predicate-aware templates, repetition avoidance, verb conjugation) → DB-loaded default responses
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
- **Phase 4:** Low priority ✅ (using var, Random consolidation, lazy EnsureCreated, DbPath env var, InMemoryDbFixture cleanup, ConjugateVerb was/were, bye exit cleanup, dictionary_definition_saved wiring)
- **Phase 5:** British English ✅ (tokeniser renaming, 91 British word variants in pos_dictionary.json)
- **Phase 6:** Simple Mathematics ✅ (IMathEngine/SimpleMath with +,-,*,/,^, regex-based, stated-result correction)
- **Phase 7:** Self-Learning Dictionary ✅ (WordDefinition/WordLink entities, definition query/learn, thesaurus, link creation)
- **Phase 8:** Noun Categorisation ✅ (NounCategoriser with DB + heuristics, auto-learn, noun-aware follow-ups)
- **Phase 9:** Proactive Conversation ✅ (dead-end question generation from user facts, predicate-aware templates, repetition avoidance via RecentlyUsedFacts rolling window)
- **Phase 10:** Phrasing Improvement ✅ (ConjugateVerb helper for 3rd-person present tense, template rewrite removing false enthusiasm/"related to" assumption/"they" pronoun across all bot response categories)
- **Phase 11:** Plural Handling (Pluraliser utility, auto-learn plurals, plural-aware POS tagging)
- **Maintenance & Cleanup (Post-Phase 11):** Code review batch fix — 10 issues resolved (NounCategoriser eager Save, duplicated path resolution, dead ProperNoun enum, N+1 query in GetResponsesForRule, HandleNameInput hardcoded greetings, HandleClarification code collapse, private IsPunctuation wrappers removed, shared TestDataHelper for seed data, Moq dependency removed, double-dispose test pattern fixed)

## Known Fixes
- **Math operators in tokeniser:** `+`, `-`, `*`, `/`, `^` are extracted as standalone tokens by Tokeniser regex. `GetUnknownWords` in `SpellChecker` must skip math operators to prevent false unknown-word prompts before math evaluation. Fixed via `SpellChecker.MathOperators` HashSet.
- **Solution file path:** `PokeChat.slnx` must use `tests/PokeChat.Tests/PokeChat.Tests.csproj` (not `../tests/...`) — the `..` resolved to a stale project copy at `/mnt/Storage/RiderProjects/tests/`.
- **Re-seeding after new categories:** `SeedBotResponses` and all other `Seed*` methods check `if (context.X.Any()) return;`. When new categories or responses are added to the seeder, existing `pokechat.db` must be deleted to get the new seed data.
- **NounCategoriser:** Instance-based, injected into ChatSession. Lookup chain: DB → common names set → place suffixes → "thing" default. Auto-learns on heuristic match (persists to noun_categories table). Used in ChatSession.ProcessSentence after SVO extraction to set SubjectCategory/ObjectCategory context keys.
- **Context follow-up loop:** `LastSubject` is never cleared when user gives minimal responses ("no", "yes"). Context follow-up fires every turn, permanently blocking proactive question generation. Fix: `ContextFollowUpCount` counter (context key) incremented each time follow-up fires, reset on SVO-bearing input. After 3 consecutive follow-ups without SVO, skip to proactive generation.
- **ConjugateVerb:** `ResponseEngine.ConjugateVerb()` applies English 3rd-person singular present tense rules (like→likes, have→has, go→goes, -y→-ies, -s/-sh/-ch/-x/-z/-o→+es). Used in `BuildProactiveQuestion`, `existing_fact`, and `random_fact_followup` paths. Only applies for third-person subjects (not I/you/we/they).
- **Template rewrites (Phase 10):** All `context_followup_with_object` templates removed "related to" assumption. All `random_fact_followup` and `proactive_general_fact` templates removed "they" pronoun misuse. `proactive_preference` and `proactive_belief` removed false enthusiasm. `existing_fact` replaced ambiguous "it" reference.
- **Pluraliser:** `NLP/Pluraliser.ToSingular()` returns candidate singular or null. Used in SpellChecker.GetUnknownWords (skip plurals of known words), PosTagger.GetTag (plural noun detection), ChatSession.ProcessSentence (auto-learn plural forms). Only validates against dictionary — "james"→"jame" rejected since "jame" isn't known.
- **Bye no longer exits:** `bye`, `goodbye`, `see you`, `good night` were removed from `bot_commands` exit commands — they now trigger farewell response rules. Only `quit` and `exit` exit the program.
- **Exit commands:** Only `quit` and `exit` are exit commands (reduced from 6).
- **POKECHAT_DB_PATH:** Environment variable overrides the SQLite database path.
- **ConjugateVerb was/were:** `was` → `was`, `were` → `were` added to irregular forms to prevent "wases" or "weres" corruption.
- **ChatSession implements IDisposable:** Required for `using var` in Program.cs.
- **InMemoryDbFixture deleted:** Was unused; all tests use FreshDbContext.
- **NounCategoriser eager Save removed:** `NounCategoriser.CategoriseNoun` no longer calls `Save()` after auto-learn. Callers own the save boundary via `KnowledgeStore.Save()`.
- **ResolveDbPath/ResolveDataFilePath dedup:** `Program.cs` now has a single `ResolveProjectRoot()` method used by both path resolvers.
- **ProperNoun enum removed:** `NLP/PosTagger.cs` had a dead `ProperNoun` value — removed.
- **GetResponsesForRule N+1 fix:** Includes `ResponseRuleResponses` in the query via `.Include(r => r.Responses)`.
- **HandleNameInput DB-driven greetings:** Now loads greeting words from `greeting_words` table — no more hardcoded `"hi"`/`"hello"` fallback.
- **HandleClarification collapsed:** Redundant else-if for `word == null` folded into preceding null-coalescing check.
- **IsPunctuation wrappers removed:** `PosTagger` and `SpellChecker` now call `PunctuationHelper.IsPunctuation` directly.
- **TestDataHelper shared seed data:** BotResponse and POS seed data extracted to `tests/PokeChat.Tests/Helpers/TestDataHelper.cs`, used by both `ChatSessionTests` and `ResponseEngineTests`.
- **Moq dependency removed:** `tests/PokeChat.Tests/PokeChat.Tests.csproj` no longer lists `Moq` (was unused).
- **Dispose test pattern fixed:** `Dispose_DoesNotThrow` no longer wraps `db` in `using` that would double-dispose the shared `PokeChatDbContext`.

## Routines
- **Code review after every change:** After each modification, review the changed code for bugs and duplicate code — refactor any duplication found.
- **When creating a new phase plan:** Append to `.agents/plan.md`, file the plan to MemPalace (`wing: pokechat, room: plans`), and update this file's Improvement Plan section.
- **After each phase or significant milestone:** Update `README.md` to reflect current architecture, completed phases, and any relevant changes.

## Git
- `.gitignore` excludes `/bin`, `/obj`, `/graphify-out`
- `pokechat.db` IS gitignored now
- `mempalace.yaml` and `entities.json` are gitignored
