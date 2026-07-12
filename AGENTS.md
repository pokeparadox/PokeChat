# PokeChat — Agent Notes

## Project
- C# .NET 10 (`net10.0`), two-project solution (`PokeChat.slnx`)
- **`Api/PokeChat.Api.csproj`** — core library (Web SDK): ChatEngine, NLP, Knowledge, Data, Responses, Math, LLM, ML, MCP, Tools, Stories, Migrations
- **`PokeChat.csproj`** — thin console HTTP client (~70 lines), calls the REST API
- SQLite via **`Microsoft.EntityFrameworkCore.Sqlite`** (EF Core, not raw `Microsoft.Data.Sqlite`)
- **`Facet`** + **`Facet.Extensions.EFCore`** for entity-to-model mapping (`[Facet(typeof(FactEntity))]` partial class)

## Commands
```bash
dotnet build                              # build all projects
dotnet run --project Api/                 # start the REST API (default http://localhost:5000)
dotnet run                                # start the console HTTP client (connects to API)
dotnet test                               # run all tests
dotnet ef migrations add <Name> --project Api/  # add a new schema migration
dotnet ef migrations remove --project Api/      # undo last migration (if unapplied)
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
                                  BotCommand, Misspelling, BotResponse, WordDefinition, WordLink, NounCategory,
                                  ConversationMetric, ResponseEffectiveness
```

## Key Details
- **DB location:** `pokechat.db` in project root (resolved by walking up from `BaseDirectory` to find `PokeChat.Api.csproj`); override via `POKECHAT_DB_PATH` environment variable
- **DB init:** `DatabaseInitializer` in `ChatSession()` constructor. Uses EF Core migrations (`Database.Migrate()`) instead of `EnsureCreated()`. On first run, applies `InitialCreate` migration to create all tables. Detects legacy databases from the `EnsureCreated` era and seeds `__EFMigrationsHistory` to preserve existing data.
- **Seeder:** `DbSeeder.Seed()` populates greetings, greeting words, response rules, POS dictionary (from `pos_dictionary.json`), name patterns, bot commands, misspellings, and bot responses on first run
- **Knowledge extraction:** "my name is Alice" → (user, is_named, Alice); "I like pizza" → (user, likes, pizza); "the sky is blue" → (sky, is, blue) [general knowledge]
- **Pronoun resolution:** ContextTracker resolves "it/this/that" → last object, "he/she" → last subject, "they/their" → last object (then last subject), "him/her/them" → last object
- **Response flow:** unknown word check → math evaluation → dictionary/thesaurus query → link creation → pattern match from DB rules → check existing facts (verb conjugated via ConjugateVerb) → context follow-up → random user fact → proactive question from user facts (predicate-aware templates, repetition avoidance, verb conjugation) → DB-loaded default responses
- **PosTagger:** Instance-based (implements `IPosTagger`), initialized from `pos_dictionary` table; no hardcoded dictionary in code
- **Response rules:** Loaded from `response_rules` + `response_rule_responses` tables (regex patterns with responses), merged with `learned_response_rules` (confidence-based preference)
- **Bot responses:** ResponseEngine templates (defaults, follow-ups, clarification prompts) stored in `bot_responses` table, loaded at construction time
- **Greeting learning:** When user responds to name prompt with a novel first word, it's learned as a greeting word
- **Name extraction:** Uses `name_patterns` table (e.g. "my name is", "i am", "call me") to extract names from input
- **Bot commands:** Exit commands loaded from `bot_commands` table (`quit`, `exit`, etc.)
- **ChatSession:** Implements `IDisposable` to clean up the DbContext
- **NLP interfaces:** All NLP components implement interfaces (`ITokeniser`, `IPosTagger`, `ISentenceSplitter`, `ISvoExtractor`) for testability
- **SpellChecker:** Levenshtein-based spell correction with `misspellings` table for known errors; `pos_dictionary` as known word dictionary
- **KnowledgeStore.Save():** Batch save method replaces per-operation SaveChanges; callers call `Save()` at logical boundaries
- **Rate limiting:** Token bucket per IP (`TokenBucketStore`/`ITokenBucketStore`), configurable costs per request type. `SessionQuotaOptions` controls per-user session cap, per-session turn cap, and per-session upstream LLM call cap. Defaults: 60 tokens/min, 50 max sessions, 10 max sessions per user, 100 turns per session, 20 upstream calls per session.
- **Database recovery:** `DatabaseInitializer` auto-backs up `pokechat.db` → `pokechat.db.bak` on every startup. On schema mismatch or migration failure, automatically recreates the DB and copies learned data from backup. `--restore-db` CLI flag restores from backup manually. `BackupHelper` uses SQLite ATTACH+INSERT for cross-schema data copy.

## DB Schema
- `users` — id, name (unique), first_seen, last_seen
- `facts` — id, user_id (nullable FK→users), subject, verb, object, predicate_type, created_at
- `conversations` — id, user_id (nullable FK→users), user_input, bot_response, timestamp, session_id, response_category
- `greetings` — id, text, is_system, created_at
- `greeting_words` — id, word (unique), learned_from_user_id (nullable FK→users), created_at
- `response_rules` — id, pattern, input_type, is_active, created_at
- `response_rule_responses` — id, rule_id (FK→response_rules, CASCADE), response_text
- `pos_dictionary` — id, word, word_type, created_at
- `name_patterns` — id, pattern, created_at
- `bot_commands` — id, command (unique), created_at
- `user_bot_names` — id, user_id (unique FK→users), bot_name, created_at
- `bot_rename_patterns` — id, pattern, created_at
- `misspellings` — id, wrong_word (unique), correction, created_at
- `bot_responses` — id, category, response_text, created_at
- `temporal_expressions` — id, expression (unique), days_offset, is_range
- `learned_response_rules` — id, pattern, response_template, input_type, learned_from_user_id (nullable FK→users), confidence, is_active, created_at
- `response_feedback` — id, rule_id, is_learned_rule, user_id (FK→users), feedback, correction_text, created_at
- `word_definitions` — id, word, definition, defined_by_user_id (nullable FK→users), created_at
- `word_links` — id, source_word, target_word, link_type, created_by_user_id (nullable FK→users), created_at
- `conversation_metrics` — id, user_id, session_id, turn_count, facts_learned, dominant_sentiment, sentiment_trend, topics_discussed, bot_response_stats, avg_response_length, session_length, started_at, ended_at
- `response_effectiveness` — id, category (unique), avg_session_length_after, used_count, follow_up_rate, last_used

## Skills
- `.skills/grammar-bot-testing.md` — reusable script for running the bot through conversation scenarios and analysing responses for grammar/natural flow bugs.

## Improvement Plan
Completed phases in `.agents/history.md` and MemPalace (`wing: pokechat, room: phase-summaries`). Plans in MemPalace (`wing: pokechat, room: plans`); no files.
### Planned (in order)

- **Sub-plan 5:** Alternative UI

## Known Fixes
Full history in MemPalace (`wing: pokechat, room: known-fixes`). Essentials only here:

- **ContractionExpander:** Loaded from `contractions` table. Uses lowercase expansion text since tokeniser lowercases.
- **Math operators in tokeniser:** `SpellChecker.MathOperators` HashSet prevents false unknown-word prompts before math eval.
- **Solution file path:** `PokeChat.slnx` uses `tests/PokeChat.Tests/PokeChat.Tests.csproj` (no `../`).
- **Re-seeding:** All `Seed*` methods check `if (context.X.Any()) return;`. Clear the relevant table to re-seed.
- **Context follow-up loop:** After 3 consecutive non-SVO responses, skip to proactive generation via `ContextFollowUpCount`.
- **ConjugateVerb:** 3rd-person singular (like→likes, have→has, etc.). Only for third-person subjects.
- **Bye no longer exits:** Only `quit` and `exit` exit the program.
- **Clarification cancel:** `IsClarificationCancelled()` checks for typo/never mind/etc. before learning.
- **POKECHAT_DB_PATH:** Environment variable overrides SQLite path.
- **Garbage triple filter:** `FunctionWords` (not, never, no) filter single-function-word object triples.
- **JokeStartPhrases:** `"funny"` removed — too broad.
- **Console.WriteLine in ChatEngine:** Replaced with `OnStatusUpdate` callback — engine must not depend on Console directly.
- **Guest name bug:** `SessionManager.GetOrCreate` called `EstablishDefaultUser("Guest")` which set `_currentUserId`, permanently blocking `HandleNameInput`. Gate now also checks `_currentUserName == "Guest"`.

## Routines
- **Code review after every change:** After each modification, review the changed code for bugs and duplicate code — refactor any duplication found.
- **Record build warnings as todos:** Any compiler/build warnings should be filed to MemPalace (`wing: pokechat, room: ideas`) as TODOs for future cleanup. Do not ignore them.
- **When creating a new phase plan:** File the plan to MemPalace (`wing: pokechat, room: plans`).
- **When a phase is completed:** File detailed completion to MemPalace (`wing: pokechat, room: phase-summaries`), append a one-line summary to `.agents/history.md`.
- **After each phase or significant milestone:** Update `README.md` only if user-facing changes (new features, CLI commands, DB schema visible to end users).
- **When a new TODO/basic idea arises:** File it to MemPalace (`wing: pokechat, room: ideas`) instead of `.agents/todo.md`. Convert to a detailed plan in `wing: pokechat, room: plans` before implementing.
- **Run log analysis during cleanup:** After completing a phase, scan `logs/*.log` for response abnormalities. Read each log file and inspect every `### Bot` response for known bad patterns:
  - **Spell checker false positives** — common short words flagged as unknown (hi→he, oh→of, why→way, ate→age, later→late, really→reality, everything→N/A)
  - **Garbage follow-ups** — bot asking about function-word objects ("not and any", "i someti ames and trains")
  - **Interview Mode** — 0 facts / 0 rules at session end, Interviewer messages getting spell-checked or unknown-word-blocked
  - **Magic 8 Ball** — firing on non-prediction questions ("Can I have a banana?")
  - **Identity issues** — name not established (first input treated as name), identity loop ("Tell me about yourself, bob" → "bob" → same)
  - **Story/poem slot garbage** — modal verbs ("mighting"), adjective/noun swaps ("searched bison"), LLM interpolation failure
  - **Emoji overuse** — too many emoji per response, inappropriate category emoji
  - **Context loop** — same follow-up repeating across 5+ turns
  - **Empty/nonsense responses** — bot says nothing useful, single-word dead-ends
  - If any new abnormality category emerges (not already in MemPalace or `.agents/history.md`), document the symptoms, affected logs, root cause hypothesis, and proposed fix as a plan to MemPalace (`wing: pokechat, room: plans`).
  - After all abnormalities are documented (plans created or fixes applied), delete the log files from `logs/`.

## Git
- `.gitignore` excludes `/bin`, `/obj`, `/graphify-out`
- `pokechat.db` IS gitignored now
- `mempalace.yaml` and `entities.json` are gitignored
