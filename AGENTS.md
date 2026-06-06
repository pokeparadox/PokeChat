# PokeChat — Agent Notes

## Project
- C# console app, .NET 10 (`net10.0`)
- Single project: `PokeChat.csproj` (solution: `PokeChat.slnx`)
- SQLite via **`Microsoft.EntityFrameworkCore.Sqlite`** (EF Core, not raw `Microsoft.Data.Sqlite`)
- **`Facet`** + **`Facet.Extensions.EFCore`** for entity-to-model mapping (`[Facet(typeof(FactEntity))]` partial class)
- Dependencies: `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Design`, `Facet`, `Facet.Extensions.EFCore`

## Commands
```bash
dotnet build                          # build
dotnet run                            # run the chat application
dotnet test                           # run all tests
dotnet ef migrations add <Name>       # add a new schema migration
dotnet ef migrations remove           # undo last migration (if unapplied)
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
- **DB location:** `pokechat.db` in project root (resolved by walking up from `BaseDirectory` to find `PokeChat.csproj`); override via `POKECHAT_DB_PATH` environment variable
- **DB init:** `DatabaseInitializer` in `ChatSession()` constructor. Uses EF Core migrations (`Database.Migrate()`) instead of `EnsureCreated()`. On first run, applies `InitialCreate` migration to create all tables. Detects legacy databases from the `EnsureCreated` era and seeds `__EFMigrationsHistory` to preserve existing data.
- **Seeder:** `DbSeeder.Seed()` populates greetings, greeting words, response rules, POS dictionary (from `pos_dictionary.json`), name patterns, bot commands, misspellings, and bot responses on first run
- **Knowledge extraction:** "my name is Alice" → (user, is_named, Alice); "I like pizza" → (user, likes, pizza); "the sky is blue" → (sky, is, blue) [general knowledge]
- **Pronoun resolution:** ContextTracker resolves "it/this/that" → last object, "he/she/they" → last subject; "him/her/them" → last object
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
A completed improvement history is maintained in `.agents/history.md`. Active plans are in `.plans/`; deferred plans in `.backlog/`, ordered by priority:
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
- **Phase 11:** Plural Handling ✅ (Pluraliser utility, auto-learn plurals, plural-aware POS tagging)
- **Maintenance & Cleanup (Post-Phase 11):** Code review batch fix — 10 issues resolved (NounCategoriser eager Save, duplicated path resolution, dead ProperNoun enum, N+1 query in GetResponsesForRule, HandleNameInput hardcoded greetings, HandleClarification code collapse, private IsPunctuation wrappers removed, shared TestDataHelper for seed data, Moq dependency removed, double-dispose test pattern fixed)
- **Phase 12:** Bot Renaming ✅ (per-user bot name stored in `user_bot_names` table, rename intent detected via `bot_rename_patterns`, 85% acceptance with 15% rejection/suggestion)
- **Phase 13:** EF Core Migrations ✅ (replaced `EnsureCreated` with `Database.Migrate`, `DatabaseInitializer` handles legacy DB transition, data survives schema upgrades)
- **Phase 14:** Reset / Start Fresh ✅ (detect "can we start afresh" patterns, warn → confirm → wipe all user data, preserve system seed, reset user identity)
- **Phase 15:** Emotion / Sentiment Awareness ✅ (EmotionKeyword entity, ~95 seed keywords across 5 sentiments, AnalyseSentiment in KnowledgeStore, sentiment stored on facts, empathy response categories in ResponseEngine, emotion_followup on sentiment change, 7 new tests)
- **Phase 16:** Contractions Handling ✅ (ContractionEntity, ContractionExpander, 54 seeded contractions, missing POS words added to pos_dictionary.json, expansion before tokenisation, 15 new tests)
- **Phase 16 (Temporal Knowledge):** Temporal Knowledge ✅ (TemporalExpression entity + 15 seeded time expressions, FactEntity.TimeContext/MentionedAt columns, ExtractTimeContext/GetFactsByTimeRange/GetFactsWithTimeContext in KnowledgeStore, time context extraction in ChatSession, temporal query handling in ResponseEngine, 7 new tests)
- **Phase 17:** Inference / Simple Reasoning ✅ (Category chain via is_a WordLinks, contradiction detection for like↔hate, generalisation inference with 50% display chance, 5 new KnowledgeStore methods, 6 inference response categories, 12 new tests)
- **Phase 18:** Session Summarisation ✅ (SessionId on conversations, ConversationSession entity, summary build via fact lookup, exit recap, 9 new tests)
- **Phase 19:** Self-Learning Response Patterns ✅ (learned_response_rules + response_feedback tables, correction detection in ChatSession, confidence-based rule selection in ResponseRules, duplicate check via Local+DB, fix pre-existing bug in "not what I meant" feedback pattern not matching)
- **Fix: Broken Conversation Flow (Post-Phase 19):** Dead `if` block removed from `HandleClarification`, context set after clarification (learned word becomes active topic), garbage SVO triple filter for General predicates with function-word objects ("not", "never", "no")
- **Fix: Missing Contractions (Post-Phase 20):** Added 9 missing `'s` contractions (`that's`, `there's`, `here's`, `what's`, `who's`, `where's`, `why's`, `how's`, `when's`) to seed data (45→54). Added `SpellChecker.IsContractionOfKnownWord` for dynamic detection — checks if unknown words match contraction patterns `'s`, `n't`, `'ll`, `'ve`, `'re`, `'m`, `'d` where root is a known dictionary word. Works for all databases, existing and new.
- **Phase 22:** Conversation Quality Metrics ✅ (ConversationMetric/ResponseEffectiveness entities, session-level metrics recorded on exit, per-category response effectiveness tracking with FollowUpRate, `GetBestPerformingCategories` for adaptive response weighting, 4 new tests, 223/223 pass)
- **Phase 23:** Grammar & Natural Flow Bugs ✅ (11 bugs fixed: greeting-as-name, conjugated verb recognition, neutral sentiment followup, empathy-first flow, proactive template subject mismatch, cross-turn inference persistence, SVO gerund splitting, sentiment intensity timing, summary verb conjugation, factual "feel that way", temporal past tense)
## Known Fixes
- **ContractionExpander:** Loaded from `contractions` table via `KnowledgeStore.GetContractions()`. Expands contracted forms before tokenisation using regex replace with `IgnoreCase`. The expander uses lowercase expansion text (`"i am"`, not `"I am"`) since the tokeniser lowercases afterward. Seeded via `DbSeeder.SeedContractions()` and `TestDataHelper.SeedContractions()`.
- **Math operators in tokeniser:** `+`, `-`, `*`, `/`, `^` are extracted as standalone tokens by Tokeniser regex. `GetUnknownWords` in `SpellChecker` must skip math operators to prevent false unknown-word prompts before math evaluation. Fixed via `SpellChecker.MathOperators` HashSet.
- **Solution file path:** `PokeChat.slnx` must use `tests/PokeChat.Tests/PokeChat.Tests.csproj` (not `../tests/...`) — the `..` resolved to a stale project copy at `/mnt/Storage/RiderProjects/tests/`.
- **Re-seeding after new categories:** All `Seed*` methods check `if (context.X.Any()) return;`. Since EF Core Migrations handle schema upgrades, the database is never deleted. To get new seed data added to an existing database, add a data migration or manually clear the relevant table.
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
- **Temporal Knowledge (Phase 16):** `ExtractTimeContext` uses `input.Contains(expression)` for matching — picks the most specific (largest absolute `DaysOffset`). Time context is stored on each fact and persisted in `CurrentTimeContext` context key. Temporal query response rule ("what did I do yesterday") is a regex pattern matched in `ResponseEngine.HandleTemporalQuery` before the generic rule engine.
- **Bot Renaming (Phase 12):** Per-user bot names stored in `user_bot_names` table. Rename patterns in `bot_rename_patterns` table (seeded: "can i call you", "i'll call you", "i will call you", "your name is"). Detection in `ChatSession.TryHandleBotRename` runs after user identity established. 85% acceptance rate; rejection triggers either a suggestion (from {Zara, Nova, Echo, Pixel, Azure, Kai, Rex}) or asks for another. `GreetingPool.GetRandomGreeting` now takes a `botName` parameter and replaces `{BOTNAME}` / `"PokeChat"` with the current name. Console output labels use `_botName`. Response categories: `bot_rename_accepted` (3 templates), `bot_rename_rejected` (2), `bot_rename_suggestion` (3).
- **Reset / Start Fresh (Phase 14):** `ChatSession.TryHandleResetRequest` detects 12 trigger phrases (e.g. "start fresh", "start afresh", "reset everything") via `Contains` on lowercased input. First match sets `PendingReset` context key and returns warning from `bot_reset_warning`. Second call with affirmation (yes/sure/ok) calls `KnowledgeStore.ResetAllUserData()` — bulk deletes from 9 tables via `ExecuteSqlRaw`, keeping system seed data intact. Clears `_currentUserId` so bot asks for name again. `_context.Clear()` resets conversation context. Negation/other input cancels without deletion. Response categories: `bot_reset_warning` (2), `bot_reset_confirmed` (2), `bot_reset_cancelled` (2).
- **Inference (Phase 17):** `DetectContradiction` finds existing facts with same subject + same object + opposite verb (like↔hate, love↔dislike). Only runs for Preference/Dislike predicates. Contradiction detection blocks fact storage and sets `LastContradiction` context key. Generalisation inference runs after contradiction check, sets `InferredGeneralisation` context key, displayed at 50% chance. `HandleInferenceResponse` in ResponseEngine fires before rule matching to catch contradictions. `GetCategoryChain` uses BFS with `visited` HashSet to prevent cycles. Inference seed data uses `is_a` link type in WordLinks table.
- **Response Rules (Phase 19):** `ResponseRules.MatchRule` merges seeded rules (`response_rules` + `response_rule_responses`) with `learned_response_rules` (loaded from DB, ordered by confidence descending). Learned rules with confidence >= 7 are preferred over seed rules (confidence 8). Learned rules start at confidence 5; each successful match +1 (cap 10); each negative feedback -2 (floor 1, deactivates at 0). `ResponseRuleRecord` has `RuleId`, `IsLearned`, `Confidence`.
- **Correction Detection (Phase 19):** `ChatSession.TryHandleCorrection` runs after rename check, before sentiment analysis. Detects `you should say X`, `say X instead`, `try saying X`, `when/if I say X you should/could Y` via regex on original input with `IgnoreCase`. Learns new response pattern by extracting the last word from the previous input as a `\bword\b` regex pattern. Negative feedback ("that's not right" etc.) records negative feedback on last rule used. Positive feedback ("that's better" etc.) records positive feedback. `LastRuleId` and `LastRuleIsLearned` context keys set by ResponseEngine after rule matching.
- **Learned rules storage (Phase 19):** `learned_response_rules` table (Pattern, ResponseTemplate, InputType, LearnedFromUserId FK→users, Confidence 1-10 default 5, IsActive default true, CreatedAt). `response_feedback` table (RuleId, IsLearnedRule discriminator, UserId FK→users, Feedback string, CorrectionText, CreatedAt). `KnowledgeStore.LearnResponseRule` checks Local then DB for duplicates. `GetLearnedRules` returns only active rules ordered by Confidence desc. `AdjustConfidence` clamps 1-10, sets IsActive=false at 1. `RecordFeedback` stores feedback and adjusts confidence.
- **Garbage triple filter (Post-Phase 20):** `ChatSession.FunctionWords` (`"not"`, `"never"`, `"no"`) filters triples in `ProcessSentence` where `predicateType == PredicateType.General` and `resolvedObject` is a single function word. Prevents multi-verb sentences like "I think you don't know" from producing garbage triples like (you, do, not) which would generate nonsensical context follow-ups.
- **"ok" in POS dictionary (Post-Phase 20):** `"ok"` was missing from `pos_dictionary.json` (`"okay"` was present at line 4411). Levenshtein suggested `"of"` (distance 1). Fixed by adding `{"Word": "ok", "Type": "adjective"}` to the JSON after `"okay"`.
- **Sentiment follow-up acknowledgement (Post-Phase 20):** After `emotion_followup` asked "Are you feeling better now?", the answer was ignored — `HandleSentiment()` skipped mild emotions (intensity < 2), falling through to context follow-up. Fixed via `PendingSentimentFollowUp` context key — set in `HandleSentiment` when `emotion_followup` fires, checked in `GenerateResponse` before sentiment analysis. If set + intensity ≥ 1, returns sentiment-aware acknowledgement (positive/negative/fallback templates). Cleared after acknowledgement.

## Routines
- **Code review after every change:** After each modification, review the changed code for bugs and duplicate code — refactor any duplication found.
- **When creating a new phase plan:** Append to `.agents/history.md` (completed history), create new phase file in `.plans/`, file the plan to MemPalace (`wing: pokechat, room: plans`), and update this file's Improvement Plan section.
- **After each phase or significant milestone:** Update `README.md` to reflect current architecture, completed phases, and any relevant changes.

## Git
- `.gitignore` excludes `/bin`, `/obj`, `/graphify-out`
- `pokechat.db` IS gitignored now
- `mempalace.yaml` and `entities.json` are gitignored
