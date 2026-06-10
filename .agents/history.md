# PokeChat — Improvement Plan

Phased plan ordered from highest to lowest priority. Each phase must build successfully before moving to the next.

---

## Phase 0 — Foundation ✅
- [x] Confirm `dotnet build` and `dotnet test` pass cleanly before any changes

---

## Phase 1 — Critical Bug Fixes ✅
- [x] `KnowledgeStore.GetFact()` loads all facts into memory (client-side filtering via `.SelectFacet()` before `.Where()`)
- [x] Proper noun detection is dead code (tokenizer lowercases before POS tagger checks `char.IsUpper()`)
- [x] Abbreviation detection in `SentenceSplitter` broken (period included in abbreviation comparison)
- [x] Pronoun resolution for "him"/"her" resolves to subject instead of object
- [x] Conversations stored with empty `BotResponse`

---

## Phase 2 — High Priority ✅
- [x] Batch `SaveChanges()` calls in `KnowledgeStore` (remove from individual methods, expose `Save()`)
- [x] Eliminate global mutable static state in `PosTagger` (convert to instance class with `IPosTagger`)
- [x] Fix schema-entity mismatch for `Misspelling` (entity property `WrongWord` vs schema column)
- [x] Remove duplicate and useless POS entries (`stop_word` type)
- [x] `ClassifyPredicate` string literals → `PredicateType` enum
- [x] Context keys → `ContextKeys` constants class
- [x] `Microsoft.EntityFrameworkCore.Design` → `PrivateAssets=all`

---

## Phase 3 — Medium Priority ✅
- [x] `PosTagger.Tag()` return type from `Dictionary` to list-based map
- [x] Consolidate duplicate `IsPunctuation` into `PunctuationHelper`
- [x] Consolidate test DB helpers (`FreshDbContext` + `InMemoryDbFixture` → one)
- [x] Add interfaces to NLP classes for testability (`ITokenizer`, `ISentenceSplitter`, `ISvoExtractor`)
- [x] `IsStopWord` HashSet → static readonly field
- [x] Fill test coverage gaps
- [x] Extract POS dictionary to `Data/pos_dictionary.json`
- [x] ResponseEngine hardcoded strings → DB-driven (`bot_responses` table)

---

## Phase 4 — Low Priority (Polish) ✅
Low-priority polish and minor improvements.

- [x] `Program.cs` → `using var` (replace `try/finally` with `using var session = new ChatSession()`)
- [x] Consolidate `Random` usage (use `Random.Shared` instead of instance `new Random()`)
- [x] `Database.EnsureCreated()` → lazy/deferred (move out of constructor, call once at startup)
- [x] Evaluate date storage format (ISO 8601 strings vs `DateTime` with value converters) — **Keep `string`**: CreatedAt is never read/compared/filtered/parsed in production code (pure audit trail). ISO 8601 strings sort lexicographically, need no value converters, no migration. Standardised format strings to `"o"` across codebase.
- [x] `DbPath` resolution robustness (fallback to environment variable, graceful failure)

---

## Phase 5 — British English Adoption (code + data)

Adopt British English spelling conventions throughout the codebase and provide both British and American variants in the seed data.

### 5.1 Rename code identifiers (mechanical)
- `ITokenizer` → `ITokeniser`, `Tokenizer` → `Tokeniser`, `Tokenize()` → `Tokenise()`
- `Initialize()` → `Initialise()` in `SpellChecker`
- Update all variables, fields, comments referencing these
- Rename `NLP/Tokenizer.cs` → `NLP/Tokeniser.cs`, `NLP/ITokenizer.cs` → `NLP/ITokeniser.cs`
- Rename test file `TokenizerTests.cs` → `TokeniserTests.cs`, update test class/method names
- **No DB changes**
- **Verify:** `dotnet build && dotnet test`

### 5.2 Seed British English dictionary data
- Add ~60–80 common British English word variants to `Data/pos_dictionary.json` alongside existing American ones
  - `colour`, `favourite`, `centre`, `programme`, `organise`, `realise`, `recognise`
  - `apologise`, `analyse`, `catalogue`, `dialogue`, `defence`, `travelling`, `jewellery`
  - `behaviour`, `labour`, `neighbour`, `honour`, `flavour`, `harbour`, `rumour`
  - `theatre`, `metre`, `litre`, `fibre`, `calibre`, `sabre`, `centre`
  - `defence`, `offence`, `licence`, `pretence`
  - `practise` (verb), `license` (verb), `advise` (verb), `devise`, `revise`, `supervise`
  - `modelled`, `labelled`, `cancelled`, `marvellous`
  - `lemmings`, `aluminium`, `speciality`
- Both British and American forms coexist in the dictionary — the POS tagger recognizes both
- **Verify:** `dotnet build && dotnet test`

---

## Phase 6 — Simple Mathematics

Add math evaluation and correction detection to the conversation flow.

### 6.1 Math engine
- New file: `Math/IMathEngine.cs` — interface with method signatures
- New file: `Math/SimpleMath.cs` — expression parser + evaluator
  - Parse arithmetic expressions: `\d+(\.\d+)?\s*[+\-*/^]\s*\d+(\.\d+)?`
  - Evaluate with floating-point (`double`), respecting operator precedence
  - Support `+`, `-`, `*`, `/`, `^` operators
  - Return result + success/failure status
- **Verify:** `dotnet build`

### 6.2 Math detection in ResponseEngine
- Before the standard response chain, check if input contains a math expression
- If yes and it's a query ("what is 2+2") → evaluate and return `math_result`
- If yes and it's a statement with `=` ("2+2=5") → verify and return `math_correction` or `math_confirmation`
- Falls through to normal flow if no math expression detected
- Narrow detection: requires digit-operator-digit to avoid false positives on natural language
- **Verify:** `dotnet build`

### 6.3 Seed math bot responses
Add to `bot_responses` seed in `DbSeeder`:
- `math_result` — "{0} = {1}"
- `math_correction` — "Actually, {0} = {1}, not {2}."
- `math_confirmation` — "That's right! {0} = {1}."
- `math_parse_error` — "I'm not sure how to calculate that. Try something like '2 + 2'."
- **Verify:** `dotnet build && dotnet run` (manual check)

### 6.4 Seed math response rules
Add to `response_rules` seed:
- `(what is|what's|calculate|compute)\s+(\d+.+)` → directs to math engine before rule matching
- **Verify:** `dotnet build`

### 6.5 Tests
- `Math/SimpleMathTests.cs` — parse, evaluate, correction, error cases
- `dotnet test` on the full suite

---

## Phase 7 — Self-Learning Dictionary

Comprehensive dictionary feature: spelling lookup, word definitions, and thesaurus functionality.

### 7.1 Database: New entities
- Create `Data/Entities/WordDefinition.cs`
  - `Id` (int PK), `Word` (string), `Definition` (string), `DefinedByUserId` (int?, FK→users), `CreatedAt` (string)
  - Multiple definitions per word allowed (no unique constraint on Word)
  - Composite index on `(Word)` for fast lookup
- Create `Data/Entities/WordLink.cs`
  - `Id` (int PK), `SourceWord` (string), `TargetWord` (string), `LinkType` (string), `CreatedByUserId` (int?, FK→users), `CreatedAt` (string)
- **Verify:** `dotnet build`

### 7.2 DbContext + DbSeeder
- Add `DbSet<WordDefinition> WordDefinitions` and `DbSet<WordLink> WordLinks` to `PokeChatDbContext`
- Fluent API config: keys, indexes, required fields, foreign keys
- Update `Schema.sql` with new tables
- **Verify:** `dotnet build`

### 7.3 KnowledgeStore: New methods
```
GetDefinitions(string word)              → List<WordDefinition>
SetDefinition(string word, string def, int? userId)
AddWordLink(string source, string target, string linkType, int? userId)
GetWordLinks(string word, string? linkType) → List<(string Word, string LinkType)>
SearchDictionary(string partial)         → List<string>
```
- **Verify:** `dotnet build`

### 7.4 New bot response categories (seed in `bot_responses`)
| Category | Example |
|----------|---------|
| `word_spelling_known` | "The word '{0}' is spelled {0}." |
| `word_spelling_suggestion` | "Did you mean '{0}'?" |
| `word_spelling_unknown` | "I don't know that word. Can you spell it for me?" |
| `definition_known` | "'{0}' can mean: 1) {1} 2) {2}" |
| `definition_unknown` | "I don't have a definition for '{0}'. What does it mean?" |
| `definition_saved` | "Thanks! I've saved that definition." |
| `definition_prompt` | "You used the word '{0}'. What does it mean?" |
| `synonyms_found` | "Words related to '{0}': {1}" |
| `synonyms_none` | "I don't know any words related to '{0}'." |
| `link_saved` | "Got it! I've linked '{0}' and '{1}'." |

### 7.5 New response rules (seed in `response_rules`)
| Pattern | Purpose |
|---------|---------|
| `(how (do you )?spell\|spell )(.+)` | Spelling request |
| `(what does\|define\|meaning of\|definition of)\s+(.+)` | Definition lookup |
| `(synonym\|similar word\|related word\|word like)\s+(.+)` | Thesaurus lookup |
| `(.+)( and\|,\s*)(.+)(are synonyms\|is (like\|similar to\|related to))` | Link creation |

### 7.6 ChatSession flow additions
- **Spelling:** In `ProcessInput`, if input matches spelling pattern → look up word in POS dict → return spelling or suggestion
- **Definition query:** Detect "what does X mean" → query `WordDefinition` table → return all definitions
- **Definition teaching:** After unknown word clarification, prompt "What does it mean?" → save response as definition
- **Thesaurus query:** Detect "words like X" → query `WordLink` → return related words
- **Link creation:** Detect "X and Y are synonyms" → save link between words

### 7.7 Tests
- New KnowledgeStore tests for `GetDefinitions`, `SetDefinition`, `AddWordLink`, `GetWordLinks`, `SearchDictionary`
- New ChatSession integration tests for spelling/definition/thesaurus flows
- `dotnet test` on the full suite

---

## Phase 8 — Noun Categorisation

Classify nouns encountered in conversation as person, place, or thing for more intelligent, context-aware responses.

### 8.1 New files
- `Core/INounCategoriser.cs` — interface: `string CategoriseNoun(string noun)`
- `Core/NounCategoriser.cs` — implementation: DB lookup → heuristics → default "thing"
- `Data/Entities/NounCategory.cs` — entity POCO (Id, Noun unique, Category, LearnedFromUserId FK→users nullable, CreatedAt)

### 8.2 Modified files
- `Data/PokeChatDbContext.cs` — add `DbSet<NounCategory>`, fluent config
- `Data/Schema.sql` — add `noun_categories` table DDL
- `Knowledge/KnowledgeStore.cs` — add `CategoriseNoun`, `AddNounCategory`, `GetNounCategories`
- `Core/ContextKeys.cs` — add `SubjectCategory`, `ObjectCategory` constants
- `Core/ChatSession.cs` — inject `INounCategoriser`, categorise SVO subject/object, detect "X is a [person/place/thing]" patterns
- `Responses/ResponseEngine.cs` — use noun category context keys for pronoun selection in follow-ups
- `Data/DbSeeder.cs` — seed ~15 noun categories + noun-category-aware bot responses

### 8.3 NounCategoriser logic
```
CategoriseNoun(noun):
  1. DB lookup → return category if found
  2. Heuristics:
     - Common first name set → "person"
     - Ends with -ville/-town/-burg/-shire/-land/-city → "place"
     - Default → "thing"
  3. Auto-learn: store (noun, category) in DB
  4. Return category
```

### 8.4 ChatSession flow
- After SVO extraction, categorise subject and object via `_nounCategoriser.CategoriseNoun()`
- Store categories in context: `ContextKeys.SubjectCategory`, `ContextKeys.ObjectCategory`
- Detect "X is a person/place/thing" patterns → learn category

### 8.5 ResponseEngine integration
- When generating follow-up templates, check subject/object category:
  - "person" → "them/him/her"
  - "place" → "there/it"
  - "thing" → "it/that"

### 8.6 Tests
- `NounCategoriserTests` — DB lookup, heuristics, default fallback, auto-learn
- Update `ChatSessionTests` — verify category context keys
- `dotnet test` on the full suite

---

---

## Phase 9 — Proactive Conversation

At conversation dead ends (default response fallback), generate meaningful questions from the user's own facts instead of generic "Interesting! Tell me more." responses.

### 9.1 ResponseEngine: Proactive question generation

Replace the `return GetRandomResponse("default_response")` fallback at the end of `GenerateResponse` with proactive question generation:

1. If `userId == null` → return `GetRandomResponse("default_response")` (no data to work with)
2. Load user's facts from DB, filter out recently used ones
3. Pick a random fact
4. Merge fact ID into `RecentlyUsedFacts` context (rolling window of 5)
5. Template selection by `PredicateType`:
   | PredicateType | bot_response category |
   |---|---|
   | `Preference` | `proactive_preference` |
   | `Dislike` | `proactive_dislike` |
   | `Possession` | `proactive_possession` |
   | `Belief` | `proactive_belief` |
   | `PersonalAttribute` | `proactive_personal` |
   | `GeneralFact` | `proactive_general_fact` |
   | `General` | `proactive_general` |
6. Format template with fact subject/verb/object
7. If no facts available → `default_response`

### 9.2 Avoid repetition

- Add `RecentlyUsedFacts` to `ContextKeys`
- Store comma-separated fact signatures (`"subject|verb|object"`)
- Filter these out when selecting a proactive fact
- Rolling window of 5 entries

### 9.3 Seed bot_responses

Add to `SeedBotResponses` in `DbSeeder`:

| Category | Example templates |
|---|---|
| `proactive_preference` | "What else do you like doing? You mentioned {0}." |
| `proactive_dislike` | "Why don't you like {0}?" |
| `proactive_possession` | "Tell me more about your {0}." |
| `proactive_belief` | "How did you learn about {0}?" |
| `proactive_personal` | "You said you're {0}. What's that like?" |
| `proactive_general_fact` | "You mentioned {0} is {1}. What do you think about it?" |
| `proactive_general` | "Tell me more about {0}." |
| `proactive_statement` | "I remember that {0} {1} {2}." |

At least 2 responses per category.

### 9.4 Tests

- Update `ResponseEngineTests` — default fallback path now produces a proactive question when user has facts
- Test: user with 0 facts gets `default_response`
- Test: recently used facts are not selected
- `dotnet test` on the full suite

---

## Phase 10 — Phrasing Improvement ✅

Fix awkward bot phrasing across all response categories: false enthusiasm ("I love that too!"), pronoun misuse ("they" for objects), forced assumptions ("related to"), ambiguous referents ("it"), and missing third-person verb conjugation.

### 10.1 Template rewrite in DbSeeder.SeedBotResponses()

| Category | Old | New |
|----------|-----|-----|
| `existing_fact` | `"... Did you know something new about it?"` | Replace with `"I already know that. Tell me something new!"` — remove ambiguous "it" |
| `context_followup_with_object` | `"You said {0} is related to {1}."` | `"Tell me more about {0} and {1}."` — remove "related to" assumption |
| `random_fact_followup` | `"Speaking of {0}, you mentioned they {1} {2}."` | `"You told me {0} {1} {2}. Tell me more!"` — remove "they" pronoun |
| `proactive_preference` | `"You like {0}? I love that too! What else?"` | `"You like {0}? What do you like most about it?"` — remove false enthusiasm |
| `proactive_belief` | `"You know about {0}? I'd love to learn more."` | `"You know about {0}? Tell me more!"` — remove false enthusiasm |
| `proactive_personal` | `"You said you're {0}. What's that like?"` | `"You said you're {0}. Tell me about it."` — neutral phrasing |
| `proactive_general_fact` | `"What do you think about it?"` | `"What do you think about that?"` — fix ambiguous "it" |

### 10.2 Add ConjugateVerb helper to ResponseEngine

Private (internal) static method applying English 3rd-person singular present tense:
- Irregulars: be→is, have→has, do→does, go→goes, say→says
- -s/-sh/-ch/-x/-z/-o→+es
- consonant+y→+ies
- No conjugation for I/you/we/they subjects

### 10.3 Wire ConjugateVerb into response paths

- `BuildProactiveQuestion`: compute `conjVerb` for `GeneralFact` category (third-person subjects)
- `GenerateResponse`: existing_fact and random_fact_followup paths pass conjugated verb
- Test: `ResponseEngine.ConjugateVerb_*` (6 unit tests + 1 integration)

### 10.4 Files modified
- `Responses/ResponseEngine.cs` — add ConjugateVerb, update BuildProactiveQuestion, existing_fact, random_fact_followup
- `Data/DbSeeder.cs` — rewrite 12 template strings across 7 categories
- `tests/PokeChat.Tests/Responses/ResponseEngineTests.cs` — add 7 tests, fix flaky assertion

### 10.5 Verify
- `dotnet build && dotnet test` — 103 tests pass

---
---

## Phase 11 — Plural Handling ✅

Add a `Pluraliser` utility that singularises English plural nouns, integrated into the NLP pipeline to prevent plural words from being treated as unknown or mis-tagged.

### 11.1 Create NLP/Pluraliser.cs

Static utility class, public method `string? ToSingular(string word)`:

1. Irregular plural dictionary (children→child, men→man, women→woman, people→person, teeth→tooth, feet→foot, mice→mouse, geese→goose, sheep→sheep, deer→deer, fish→fish, species→species)
2. -ies → -y (berries→berry), length guard: word > 4
3. -ves → -f (knives→knife)
4. -es after s/sh/ch/x/z/o → strip "es" (boxes→box)
5. -s → strip "s" (cats→cat), length guard: result ≥ 2 chars
6. Returns null when no rule applies

### 11.2 Update SpellChecker

- Add `IsPluralOfKnownWord(string token)` public method
- In `GetUnknownWords`, after `!_dictionary.Contains(token)`, check if plural of known word → skip

### 11.3 Update PosTagger

In `GetTag`, after the existing plural-verb check, add plural-noun check: singularise and look up in word tag map as Noun.

### 11.4 Update ChatSession.ProcessSentence

After `GetUnknownWords`, auto-learn any unknown word that is a plural of a known word: add to POS dictionary (via KnowledgeStore) and to SpellChecker's dictionary.

### 11.5 Tests

- `NLP/PluraliserTests.cs` (new) — regular -s, -es, -ies, -ves, irregular, non-plural returns null, short word, already singular
- `NLP/SpellCheckerTests.cs` — GetUnknownWords skips plural when singular known, IsPluralOfKnownWord returns true/false
- `NLP/PosTaggerTests.cs` — plural noun tagged as Noun, plural verb still Verb

### 11.6 Files modified

- `NLP/Pluraliser.cs` — new (~45 lines)
- `NLP/SpellChecker.cs` — add IsPluralOfKnownWord + plural skip in GetUnknownWords (~8 lines)
- `NLP/PosTagger.cs` — add noun plural heuristic in GetTag (~5 lines)
- `Core/ChatSession.cs` — auto-learn plurals in ProcessSentence (~7 lines)
- `tests/PokeChat.Tests/NLP/PluraliserTests.cs` — new (8 tests)
- `tests/PokeChat.Tests/NLP/SpellCheckerTests.cs` — 2 tests
- `tests/PokeChat.Tests/NLP/PosTaggerTests.cs` — 2 tests

### 11.7 Verify
- `dotnet build && dotnet test` — all pass

---

## Maintenance & Cleanup (Post-Phase 11) ✅

Review-driven fixes applied alongside Phase 11:
- ConjugateVerb handles was/were, exit commands reduced to 2, dictionary_definition_saved wired from DB, InMemoryDbFixture deleted, POKECHAT_DB_PATH env var
- **Code review batch:** NounCategoriser eager Save removed, path resolution dedup, dead ProperNoun enum removed, GetResponsesForRule N+1 fixed, HandleNameInput DB-driven greetings, HandleClarification collapsed, IsPunctuation wrappers removed, shared TestDataHelper, Moq removed, double-dispose fixed

---

## Phase 12 — Bot Renaming ✅

Per-user bot naming: the bot can be renamed by the user and remembers the name per user.

- [x] **B1:** New `user_bot_names` table (user_id unique FK, bot_name, created_at)
- [x] **B2:** New `bot_rename_patterns` table (pattern, created_at)
- [x] **B3:** `UserBotName` and `BotRenamePattern` entity classes
- [x] **B4:** `KnowledgeStore.GetUserBotName(userId)`, `SetUserBotName(userId, name)`, `GetBotRenamePatterns()`
- [x] **B5:** `DbSeeder.SeedBotRenamePatterns()` seeds `"can i call you"`, `"i'll call you"`, `"i will call you"`, `"your name is"`
- [x] **B6:** Three new BotResponse categories seeded: `bot_rename_accepted` (3), `bot_rename_rejected` (2), `bot_rename_suggestion` (3)
- [x] **B7:** `GreetingPool.GetRandomGreeting` takes `botName` param, replaces `{BOTNAME}`/`"PokeChat"` with current name
- [x] **B8:** `ChatSession.TryHandleBotRename` detects rename intent from patterns, extracts proposed name
- [x] **B9:** `ChatSession.HandleBotRenameProposal`: 85% accept → saves to DB, sets `_botName`; 15% reject → suggest or ask for another
- [x] **B10:** `ChatSession.GetBotRenameResponse` follows DB-first → hardcoded fallback pattern (like `GetNameIntroResponse`)
- [x] **B11:** Console output labels use `_botName` instead of hardcoded `"PokeChat"`
- [x] **B12:** `HandleNameInput` loads stored bot name from DB after user identity established
- [x] **B13:** Tests for `TryHandleBotRename`, GreetingPool bot name formatting, backwards compatibility
- [x] **B14:** 121/121 tests pass (117 existing + 4 new)

---

## Phase 13 — EF Core Migrations ✅

Replace `Database.EnsureCreated()` with EF Core Migrations so database schema changes never require deleting `pokechat.db`.

### 13.1 Create initial migration
- `dotnet ef migrations add InitialCreate` captures all 17 tables from current `OnModelCreating`
- Generates `Migrations/InitialCreate.cs`, `Migrations/2026..._InitialCreate.Designer.cs`, `Migrations/PokeChatDbContextModelSnapshot.cs`

### 13.2 Create `Data/DatabaseInitializer.cs`
- New class wrapping the migration + seed flow
- On fresh DB: `Migrate()` creates schema from scratch
- On legacy DB (from `EnsureCreated`): catches `SqliteException` from missing `__EFMigrationsHistory`, detects tables via raw SQL, seeds history for all compiled migrations, then `Migrate()` applies any remaining new migrations
- Always calls `DbSeeder.Seed()` (idempotent — checks `Any()`)

### 13.3 Update `ChatSession` constructor
- Replace `_dbContext.Database.EnsureCreated(); DbSeeder.Seed(_dbContext);` with `new DatabaseInitializer(_dbContext).Initialize();`

### 13.4 Add migration commands to `AGENTS.md`
- `dotnet ef migrations add <Name>` and `dotnet ef migrations remove`

### 13.5 Known fix update
- Replace "delete pokechat.db when adding tables" note — document that `Migrate()` handles upgrades, new seed data requires data migration or manual table clear

### 13.6 Verify
- `dotnet build` — succeeds
- `dotnet test` — 121/121 pass

---

## Phase 14 — Reset / Start Fresh ✅

Allow users to wipe all learned data and start a new conversation: "Can we start afresh?"

### 14.1 Detection and confirmation flow
- `ChatSession.ResetTriggers` — static array of 12 phrases: "start fresh", "start afresh", "start over", "reset everything", "reset all data", "forget everything", "wipe all memories", "wipe everything", "clear all data", "clear everything", "clear all memories", "fresh start"
- `ChatSession.TryHandleResetRequest` — pattern-matched via `input.Contains(trigger)` after user identity established
- First detection returns warning (`bot_reset_warning`), sets `ContextKeys.PendingReset`
- Second call with affirmation (`Affirmations.Contains`) wipes all user data; anything else cancels

### 14.2 Data wipe
- `KnowledgeStore.ResetAllUserData()` — `ExecuteSqlRaw` on 9 tables:
  - `DELETE FROM Conversations`
  - `DELETE FROM Facts`
  - `DELETE FROM WordDefinitions`
  - `DELETE FROM WordLinks`
  - `DELETE FROM GreetingWords WHERE LearnedFromUserId IS NOT NULL`
  - `DELETE FROM NounCategories WHERE LearnedFromUserId IS NOT NULL`
  - `DELETE FROM UserBotNames`
  - `DELETE FROM PosDictionary WHERE WordType = 'unknown'`
  - `DELETE FROM Users`
- Preserves all system seed data (null FK rows)
- After wipe: `_context.Clear()`, `_currentUserId = null`, `_currentUserName = ""` (bot asks for name again)

### 14.3 New bot response categories (seeded)
| Category | Templates (2 each) |
|---|---|
| `bot_reset_warning` | "This will delete all our conversations... Are you sure?" / "Are you sure you want me to forget everything?" |
| `bot_reset_confirmed` | "Done! I've forgotten everything. Let's start fresh!" / "All memories cleared. It's like we're meeting for the first time!" |
| `bot_reset_cancelled` | "Okay, nothing was deleted. Let's continue!" / "No problem, I'll keep our memories safe!" |

### 14.4 Files modified
- `Core/ChatSession.cs` — ResetTriggers, TryHandleResetRequest, GetResetResponse, wired into ProcessInput between unknown-word clear and rename check
- `Core/ContextKeys.cs` — PendingReset constant
- `Knowledge/KnowledgeStore.cs` — ResetAllUserData()
- `Data/DbSeeder.cs` — 6 seed bot responses (3 categories × 2)
- `tests/PokeChat.Tests/Helpers/TestDataHelper.cs` — 6 BotResponse entries
- `tests/PokeChat.Tests/Core/ChatSessionTests.cs` — 7 new tests
- `.agents/plan.md` — this phase

### 14.5 Verify
- `dotnet build` — succeeds
- `dotnet test` — 129/129 pass

---

## Phase 15 — Emotion / Sentiment Awareness ✅

Emotion keyword analysis and empathy responses. Analysing sentiment from user input and responding with emotion-appropriate templates.

### 15.1 New entity
- `EmotionKeyword` (Id, Word unique, Sentiment, Intensity, CreatedAt)
- `DbSet<EmotionKeyword>` in PokeChatDbContext with unique index on Word
- Seeded ~95 keywords across 5 sentiments (positive, negative, anger, fear, surprise)

### 15.2 KnowledgeStore additions
- `AnalyseSentiment(string input)` — scans for emotion keywords, returns dominant sentiment + intensity
- `GetEmotionKeywords()` — returns all keywords
- `UpdateFactSentiment(int factId, string sentiment, int intensity)` — retroactive sentiment update

### 15.3 ChatSession flow
- `AnalyseSentiment` called before sentence processing
- Sentiment stored on each fact (Sentiment, EmotionIntensity columns)
- `PreviousSentiment` / `CurrentSentiment` context keys added
- Sentiment change triggers `emotion_followup` response

### 15.4 ResponseEngine additions
- Empathy response categories seeded: `empathy_sad`, `empathy_happy`, `empathy_angry`, `empathy_afraid`, `empathy_surprised`
- `emotion_followup` category for sentiment change detection
- Response selection prioritises empathy category when sentiment detected

### 15.5 Files modified
- `Data/Entities/EmotionKeyword.cs` — new
- `Data/PokeChatDbContext.cs` — DbSet + fluent config
- `Data/Schema.sql` — DDL
- `Data/DbSeeder.cs` — SeedEmotionKeywords (~95 entries)
- `Knowledge/KnowledgeStore.cs` — AnalyseSentiment, GetEmotionKeywords, UpdateFactSentiment
- `Core/ChatSession.cs` — sentiment analysis wiring
- `Core/ContextKeys.cs` — CurrentSentiment, PreviousSentiment, LastSentimentIntensity
- `Responses/ResponseEngine.cs` — empathy response path
- `Data/Entities/FactEntity.cs` — Sentiment, EmotionIntensity columns
- `tests/PokeChat.Tests/Helpers/TestDataHelper.cs` — SeedEmotionKeywords
- `tests/PokeChat.Tests/Core/ChatSessionTests.cs` — 7 new tests

### 15.6 Verify
- `dotnet build && dotnet test` — 142/142 pass

---

## Phase 16 — Contractions Handling ✅

Enable the bot to understand common English contractions (e.g. "I'm", "they're", "don't", "can't") by expanding them during tokenisation.

### 16.1 New files
- `Data/Entities/ContractionEntity.cs` — entity (Id, Contraction unique, Expansion)
- `NLP/ContractionExpander.cs` — loaded from DB, `Expand(string input)` via Regex with IgnoreCase

### 16.2 Modified files
- `Data/PokeChatDbContext.cs` — `DbSet<ContractionEntity>`, fluent config (unique index on Contraction)
- `Data/Schema.sql` — DDL for `contractions` table
- `Data/DbSeeder.cs` — `SeedContractions()` with 44 entries
- `Data/pos_dictionary.json` — added `cannot`, `not`, `going`, `got`
- `NLP/Tokeniser.cs` — optional `ContractionExpander` constructor param, expands before regex
- `Core/ChatSession.cs` — loads contractions, creates expander, passes to Tokeniser
- `Knowledge/KnowledgeStore.cs` — `GetContractions()` method
- `tests/PokeChat.Tests/Helpers/TestDataHelper.cs` — `SeedContractions()`, 4 POS words added

### 16.3 Expansion approach
- Expansion happens before tokenisation (pre-process input)
- ContractionExpander is thread-safe after initialisation
- Expand uses `Regex.Replace` with `RegexOptions.IgnoreCase`
- Expansion text is lowercase (tokeniser lowercases after expansion)
- 44 seeded contractions covering verb+not, pronoun+verb, let's, gonna/wanna/gotta

### 16.4 Tests
- `ContractionExpanderTests` — 11 tests: per-contraction, multiple contractions, no-contraction, case-insensitive, empty
- `TokeniserTests` — 2 new tests: expansion integration, multiple contractions
- `ChatSessionTests` — 2 new integration tests: fact storage with contractions

### 16.5 Verify
- `dotnet build && dotnet test` — 157/157 pass

---

## Phase 17 — Temporal Knowledge ✅

Give the bot a sense of time. Facts are stored with temporal context so the bot can answer "what did I do yesterday?" and reference when things happened.

### New files
- `Data/Entities/TemporalExpression.cs` — entity for temporal expression lookup

### Modified files
- `Data/Entities/FactEntity.cs` — added `TimeContext` (string?) and `MentionedAt` (string) columns
- `Data/PokeChatDbContext.cs` — `DbSet<TemporalExpression>`, fluent config for new FactEntity columns and TemporalExpression unique index
- `Data/Schema.sql` — added `time_context` and `mentioned_at` to facts; new `temporal_expressions` table
- `Migrations/20260603221011_Phase16_TemporalKnowledge.cs` — EF Core migration (3rd migration after the original Phase16 naming collision)
- `Knowledge/KnowledgeStore.cs` — added `ExtractTimeContext`, `GetFactsByTimeRange`, `GetFactsWithTimeContext`, `GetTemporalExpressions`; `StoreFact` now copies `TimeContext`/`MentionedAt`
- `Data/DbSeeder.cs` — `SeedTemporalExpressions()` with 15 entries, 6 temporal bot responses, 1 temporal response rule
- `Core/ChatSession.cs` — extracts time context during sentence processing, stores on facts, persists in context tracker
- `Core/ContextKeys.cs` — `CurrentTimeContext` constant
- `Responses/ResponseEngine.cs` — `HandleTemporalQuery` method for "what did I do yesterday" patterns, wired before rule matching
- `tests/PokeChat.Tests/Helpers/TestDataHelper.cs` — `SeedTemporalExpressions()`, 6 temporal BotResponse entries, 5 new POS words

### Tests
- `KnowledgeStore.ExtractTimeContext_DetectsKnownExpression` — "yesterday" → "yesterday"
- `KnowledgeStore.ExtractTimeContext_ReturnsNull_WhenNoMatch` — "hello world" → null
- `KnowledgeStore.ExtractTimeContext_ReturnsMostSpecific` — "yesterday and last year" → "last year"
- `KnowledgeStore.GetFactsWithTimeContext_ReturnsMatchingFacts` — filters correctly
- `KnowledgeStore.GetFactsByTimeRange_ReturnsFactsInRange` — date-window filtering
- `ChatSession.TemporalFlow_DetectsAndStoresTimeContext` — integration test
- `ChatSession.TemporalQuery_ReturnsFormattedResponse` — end-to-end query
- **7 new tests, 164/164 total pass**

### Verify
- `dotnet build` — succeeds
- `dotnet test` — 164/164 pass

## Phase 18 — Inference / Simple Reasoning ✅

Bot moves from fact-recording to fact-connecting. Syllogistic reasoning, category generalisation, and contradiction detection over known facts and WordLinks.

### New methods (KnowledgeStore)
- `GetCategoryChain(string word)` — walk `is_a` WordLinks upward to find all parent categories (BFS with cycle protection)
- `GetAllOfType(string categoryWord)` — find all items linked to a category via `is_a`
- `InferPreference(int userId, string category)` — check if user has a preference fact about any member of a category
- `DetectContradiction(int userId, string subject, string verb, string obj)` — find existing fact with same subject + object but opposite verb (like↔hate, love↔dislike)
- `GetTransitiveFacts(string subject, string relation, int maxDepth)` — follow WordLink chains to find facts about connected entities

### Modified files
- `Core/ContextKeys.cs` — added `InferenceDepth`, `LastContradiction`, `InferredGeneralisation`
- `Knowledge/KnowledgeStore.cs` — 5 new inference methods
- `Core/ChatSession.cs` — inference pipeline in `ProcessSentence`: after SVO extraction for Preference/Dislike predicates, runs `DetectContradiction` (skip store if found, set `LastContradiction` context) and `GetCategoryChain` (set `InferredGeneralisation` context)
- `Responses/ResponseEngine.cs` — `HandleInferenceResponse()` checks `LastContradiction` (always returns contradiction response) and `InferredGeneralisation` (50% chance); wired in `GenerateResponse` before rule matching
- `Data/DbSeeder.cs` — `SeedInferenceWordLinks()` with 15 `is_a` links (pizza→food, coffee→drink, dog→animal, etc.), 14 inference bot response templates across 6 categories
- `tests/PokeChat.Tests/Helpers/TestDataHelper.cs` — `SeedInferenceWordLinks()`, 4 inference BotResponse entries, 3 additional POS words

### Tests (12 new, 176/176 total)
- `GetCategoryChain_Food` — pizza → food
- `GetCategoryChain_Unknown_ReturnsEmpty` — unknown word → []
- `GetAllOfType_Known` — food → [pizza, burger, pasta]
- `InferPreference_KnownCategory` — likes pizza → infer via food
- `InferPreference_NoMatch_ReturnsNull` — likes pizza → check drink → null
- `InferPreference_NoFacts_ReturnsNull` — no facts → null
- `DetectContradiction_FindsOppositePreference` — like pizza vs hate pizza → found
- `DetectContradiction_SameVerbDifferentObject_ReturnsNull` — like pizza vs like pasta → null
- `DetectContradiction_NoMatch_ReturnsNull` — no facts → null
- `GetTransitiveFacts_FindsDirectLinks` — alice→friends_with→bob finds bob's facts
- `InferenceFlow_ContradictionDetected` — integration: like pizza then hate pizza → response mentions both
- `InferenceFlow_StoresFact_WhenNoContradiction` — integration: like pizza → fact stored

### Verify
- `dotnet build` — succeeds
- `dotnet test` — 176/176 pass

---

## Phase 19 — Session Summarisation ✅

### Files Changed
- `Data/Entities/Conversation.cs` — added `SessionId` (string?) property
- `Data/Entities/ConversationSession.cs` — new entity (Id, SessionGuid, UserId, StartedAt, EndedAt, TurnCount)
- `Data/PokeChatDbContext.cs` — added `DbSet<ConversationSession>`, fluent config for both `SessionId` and `ConversationSessions`
- `Data/Schema.sql` — added `session_id` column to `conversations`, added `conversation_sessions` table
- `Data/DbSeeder.cs` — seeded `session_summary_short`, `session_summary_long`, `session_summary_empty`, `session_summary_end` response categories (8 entries)
- `Core/ChatSession.cs` — added `_sessionId` field (`Guid.NewGuid().ToString()` default), passed to `StoreConversation`, stored in context, `GenerateSessionEndSummary()` called on exit
- `Core/ContextKeys.cs` — added `SessionId = "session_id"`
- `Knowledge/KnowledgeStore.cs` — updated `StoreConversation` to accept `sessionId`, added `CreateConversationSession`, `EndConversationSession`, `GetSessionConversationCount`, `BuildSessionSummary`
- `Responses/ResponseEngine.cs` — added `HandleSessionSummaryRequest()` called at the start of `GenerateResponse`, detects summary trigger phrases before unknown word checking
- `tests/PokeChat.Tests/Helpers/TestDataHelper.cs` — seeded session summary bot responses + POS dictionary entries for trigger words (summary, we, talk, about, etc.)
- `Migrations/20260604215343_Phase18_SessionSummarisation.cs` — EF Core migration

### Key Details
- **Session ID:** A GUID assigned in the `ChatSession` constructor, stored in `Conversation.SessionId` for each turn
- **Summary triggers:** "what did we talk about", "summarise/summarize our conversation", "what have we discussed", "tell me what we talked about", "summary" (exact), "summary of..." (prefix)
- **Summary building:** `BuildSessionSummary` finds all conversations in the session, then looks up facts whose verb and object appear in the conversation input text
- **Content categories:** `session_summary_short` (1-2 facts, inline), `session_summary_long` (3+ facts, numbered), `session_summary_empty` (no facts yet), `session_summary_end` (on quit/exit)
- **Exit recap:** When user types `quit` or `exit`, `GenerateSessionEndSummary()` builds and displays the session summary before the goodbye message
- **Summary detection runs first:** The summary handler runs at the top of `GenerateResponse`, before unknown word checking, so trigger words like "summary" don't get flagged as unknown

### New Tests
- `KnowledgeStoreTests.CreateConversationSession_StoresSession` — session row created correctly
- `KnowledgeStoreTests.EndConversationSession_SetsEndedAt` — EndedAt populated after end
- `KnowledgeStoreTests.GetSessionConversationCount_ReturnsCorrectCount` — count per session ID
- `KnowledgeStoreTests.BuildSessionSummary_ReturnsEmpty_WhenNoConversations` — empty session returns empty
- `KnowledgeStoreTests.BuildSessionSummary_ReturnsFactsFromSession` — builds summary from session facts
- `ChatSessionTests.SessionSummary_DetectsSummaryRequest_AndReturnsResponse` — "what did we talk about" returns fact-based summary
- `ChatSessionTests.SessionSummary_ReturnsEmptyMessage_WhenNoFacts` — "summarise our conversation" without facts returns empty message
- `ChatSessionTests.SessionSummary_RecognizesSummaryKeyword` — bare "summary" keyword works
- `ChatSessionTests.SessionSummary_RecognizesSummaryOfPrefix` — "summary of today" prefix works

### Verify
- `dotnet build` — succeeds
- `dotnet test` — 185/185 pass

---

## Phase 20 — Self-Learning Response Patterns ✅

Bot learns new response patterns from user corrections and rephrasings. Moves beyond fact-learning into behavioural adaptation.

### New entities
- `LearnedResponseRule` (Id, Pattern, ResponseTemplate, InputType, LearnedFromUserId FK→users, Confidence 1-10 default 5, IsActive default true, CreatedAt)
- `ResponseFeedback` (Id, RuleId, IsLearnedRule, UserId FK→users, Feedback, CorrectionText, CreatedAt)

### Modified files
- `Data/PokeChatDbContext.cs` — `DbSet<LearnedResponseRule>` and `DbSet<ResponseFeedback>`, fluent config with FKs to User
- `Data/Schema.sql` — DDL for `learned_response_rules` and `response_feedback` tables
- `Data/DbSeeder.cs` — seeded `pattern_learned`, `pattern_acknowledged`, `pattern_not_clear`, `pattern_already_known` (10 total)
- `Core/ContextKeys.cs` — added `LastRuleId`, `LastRuleIsLearned`, `LastUserInput`
- `Core/ChatSession.cs` — `TryHandleCorrection` with regex on original input (`RegexOptions.IgnoreCase`):
  - `you should say X`, `say X instead`, `try saying X` → learn new response pattern
  - `when/if I say X you should/could Y` → learn pattern+response pair
  - Negative feedback ("that's not right"/"not what i meant") → record negative, adjust confidence -2
  - Positive feedback ("that's better"/"exactly") → record positive, adjust confidence +1
  - Wired into `ProcessInput` after rename check, before sentiment analysis
- `Knowledge/KnowledgeStore.cs` — `LearnResponseRule` (checks Local+DB for duplicates), `IsLearnedRuleKnown`, `GetLearnedRules` (active only, confidence desc), `AdjustConfidence` (Clamp 1-10, deactivate at 1), `RecordFeedback`
- `Responses/ResponseRules.cs` — `ResponseRuleRecord` extended with `RuleId`, `IsLearned`, `Confidence` (seed default 8). `MatchRule` merges learned rules with seeded, prefers learned if confidence >= 7
- `Responses/ResponseEngine.cs` — stores `LastRuleId` and `LastRuleIsLearned` in context after rule match
- `tests/PokeChat.Tests/Helpers/TestDataHelper.cs` — seeded correction bot responses
- Migration: `Phase19_SelfLearningResponsePatterns`

### Key Details
- **Duplicate check:** LearnsResponseRule checks `.Local` change tracker first, falls back to DB query
- **Correction regex:** Uses original `input` (not `lowerInput`) with `RegexOptions.IgnoreCase` to preserve template case, then `Trim('.', '!', '?')` strips trailing punctuation
- **Pattern extraction:** `ExtractPatternFromLastInput` takes last word from `LastUserInput`, strips punctuation, turns into `\bword\b` regex
- **Pre-existing bug fixed:** `"not what I meant"` with uppercase `I` never matched lowercased input — changed to `"not what i meant"`
- **Confidence system:** New at 5/10, successful match + (cap 10), negative feedback -2 (floor 1, deactivates `IsActive=false` at 1). Seed rules fixed at 8. Learned >= 7 beats seed.

### New Tests (11 total, 196/196 pass)
- `KnowledgeStore.LearnResponseRule_StoresAndRetrieves`
- `KnowledgeStore.LearnResponseRule_Duplicate_DoesNotStore`
- `KnowledgeStore.RecordFeedback_Positive_IncreasesConfidence`
- `KnowledgeStore.RecordFeedback_Negative_DecreasesConfidence`
- `KnowledgeStore.AdjustConfidence_ClampsToRange`
- `KnowledgeStore.IsLearnedRuleKnown_ReturnsTrue_WhenExists`
- `ChatSession.CorrectionDetection_LearnsPattern_FromYouShouldSay`
- `ChatSession.CorrectionDetection_LearnsPattern_FromSayInstead`
- `ChatSession.CorrectionDetection_NegativeFeedback_RecordsFeedback`
- `ChatSession.CorrectionDetection_PositiveFeedback_RecordsFeedback`
- `ChatSession.CorrectionDetection_WhenISay_LearnsPair`

### Verify
- `dotnet build && dotnet test` — 196/196 pass

---

## Phase 21 — Multi-Turn Topic Tracking ✅

Topic stack across 5 turns so the bot can reference older topics when context follow-up is exhausted.

### Modified files
- `Knowledge/ContextTracker.cs` — added `TopicEntry` class, `TopicStack` (max 5), `PushTopic`, `GetRecentTopics`, `GetTopicBySubject`, updated `Clear()`
- `Core/ContextKeys.cs` — added `TopicStackLength`, `LastTopicSubject`, `LastTopicObject`, `TopicReferenceCount`
- `Knowledge/KnowledgeStore.cs` — added `GetFactCountAboutSubject(int userId, string subject)`
- `Core/NounCategoriser.cs` — added `_categoryCache` dictionary to prevent duplicate NounCategory inserts within a session (fixes `UNIQUE constraint` errors when `CategoriseNoun` is called multiple times before `Save()`)
- `Core/ChatSession.cs` — added `TopicStack` internal property; calls `_context.PushTopic(...)` after each SVO triple in `ProcessSentence`
- `Responses/ResponseEngine.cs` — added `BuildTopicFollowUp()` method that scans topic stack for older topics after context follow-up exhaustion (followUpCount >= 3); wired into `GenerateResponse`
- `Data/DbSeeder.cs` — 12 new bot responses across 4 categories: `topic_reference_old` (3), `topic_reference_fact` (3), `topic_transition` (2), `topic_followup_light` (3)
- `tests/PokeChat.Tests/Helpers/TestDataHelper.cs` — 7 topic bot response entries
- `tests/PokeChat.Tests/Knowledge/ContextTrackerTests.cs` — 7 new tests: `PushTopic_AddsToStack`, `PushTopic_EvictsOldest_WhenFull`, `PushTopic_IncrementsMentionCount_OnDuplicate`, `GetRecentTopics_ReturnsCorrectCount`, `GetTopicBySubject_ReturnsTopic`, `GetTopicBySubject_NoMatch_ReturnsNull`, `Clear_EmptiesTopicStack`
- `tests/PokeChat.Tests/Core/ChatSessionTests.cs` — 4 new tests: `MultiTurnTopicFlow_PushesTopicAfterSvoExtraction`, `MultiTurnTopicFlow_MultipleTopics_AddedToStack`, `MultiTurnTopicFlow_DoesNotDuplicateTopic_OnSameInput`, `MultiTurnTopicFlow_TopicReference_ReturnsTopicResponse_WhenFollowUpExhausted`

### Verify
- `dotnet build && dotnet test` — 207/207 pass

---

## Maintenance & Cleanup ✅

Bugfix batch between major phases:

- **Broken Conversation Flow** — Removed dead `if` in `HandleClarification`. Context now set after clarification (learned word becomes active topic). Garbage SVO filter for `General` predicates with function-word objects ("not"/"never"/"no").
- **Missing Contractions** — `IsContractionOfKnownWord()` in SpellChecker for dynamic detection. 9 missing `'s` contractions added to seed (45→54). 7 new tests.
- **"ok" unknown + sentiment question ignored** — `"ok"` added to POS dict. `PendingSentimentFollowUp` context key prevents ignored sentiment check-ins. 4 new response templates.
- **SVO Auto-Learn Unknown Words** — SVO extraction runs before unknown-word detection; tokens in valid triples auto-learned. 7 affirmation words added to POS dict.
- **Context Follow-Up Natural Flow** — `context_followup_self`/`context_followup_with_object_self` categories prevent third-person user reference. `"they"/"their"` pronoun resolution prefers `LastObject`. Fallback for missing DB categories.
- **Negated Context Follow-Up** — Single-noun input sets `LastSubject` to the noun (not username). Garbage triple filter widened to cover `GeneralFact` function words.

## Phase 22 — Conversation Quality Metrics ✅

Track per-session metrics (turn count, facts learned, sentiment trend, topics, response stats) and per-category response effectiveness (follow-up rates).

### New entities
- `ConversationMetric` (session-level: TurnCount, FactsLearned, DominantSentiment, SentimentTrend, TopicsDiscussed, BotResponseStats, AvgResponseLength, SessionLength, StartedAt, EndedAt)
- `ResponseEffectiveness` (per-category: Category, AvgSessionLengthAfter, UsedCount, FollowUpRate, LastUsed)
- `ResponseCategory` column on `Conversation`

### Modified files
- `Data/PokeChatDbContext.cs` — added `DbSet<ConversationMetric>` and `DbSet<ResponseEffectiveness>`, fluent config for both
- `Data/Schema.sql` — DDL for `conversation_metrics` and `response_effectiveness` tables, `response_category` column on conversations
- `Core/ContextKeys.cs` — added `CurrentResponseCategory`, `PreviousResponseCategory`, `LastResponseHadSvo`, `AdaptiveResponseWeighting`
- `Knowledge/KnowledgeStore.cs` — `RecordSessionMetrics(sessionId)`, `UpdateResponseEffectiveness(category, hadFollowUp)`, `GetEffectiveness(category)`, `GetMetricsForUser(userId)`, `GetBestPerformingCategories(topN)`, `GetConversationsBySession(sessionId)`, overloaded `StoreConversation` with `responseCategory`
- `Responses/ResponseEngine.cs` — `GetRandomResponse` sets `CurrentResponseCategory`; rule-match path sets `"rule_match"`
- `Core/ChatSession.cs` — calls `RecordSessionMetrics(_sessionId)` before exit summary; saves `PreviousResponseCategory`/`CurrentResponseCategory` per turn; passes category to `StoreConversation`; calls `UpdateResponseEffectiveness` for previous turn if SVO-bearing
- `Data/DbSeeder.cs` — seeded `metrics_insight` (4) and `metrics_improvement` (3) response templates
- `tests/PokeChat.Tests/Helpers/TestDataHelper.cs` — matching seed data
- Migration: `Phase22_ConversationMetrics`

### Key Details
- Metrics are recorded at session end (before goodbye/summary)
- `UpdateResponseEffectiveness` uses `.Local` + DB fallback for duplicate detection
- `GetBestPerformingCategories` requires min 2 uses, ordered by FollowUpRate DESC then UsedCount DESC

### New Tests (4 total, 223/223 pass)
- `KnowledgeStoreTests.RecordSessionMetrics_StoresCorrectly` — all fields verified
- `KnowledgeStoreTests.UpdateResponseEffectiveness_IncrementsCount` — UsedCount + FollowUpRate tracking
- `KnowledgeStoreTests.GetBestPerformingCategories_ReturnsOrdered` — ordering by FollowUpRate
- `ChatSessionTests.ResponseCategory_TrackedPerTurn` — all conversations have non-null ResponseCategory

---

## Phase 23 — Grammar & Natural Flow Bugs ✅

11 bugs found by running the bot through realistic conversations. Fixed across 6 files.

### B1 — Greeting word accepted as user name (Critical)
`Core/ChatSession.cs:ExtractName` — Single-token fallback now checks `_greetingWords` before accepting as name. If greeting, returns empty string (re-ask).

### B2 — Conjugated verb forms not recognised in ClassifyPredicate (High)
`Core/ChatSession.cs:StemVerb` — New static method reverses 3rd-person singular conjugation. e.g. `"loves"`→`"love"`, `"has"`→`"have"`, `"likes"`→`"like"`. Used in `ClassifyPredicate` so preference/belief/possession verbs are correctly classified.

### B3/B4 — Emotion followup with neutral sentiment (High)
`Responses/ResponseEngine.cs:HandleSentiment` — Skips `emotion_followup` when previous sentiment is `"neutral"` (unnatural: "You seemed neutral earlier"). First emotional expression now receives direct empathy, not follow-up question. `PendingSentimentFollowUp` set after empathy for next-turn check-in.

### B5 — Proactive templates hardcode "you"/"your" (Medium)
`Responses/ResponseEngine.cs:BuildProactiveQuestion` — All categories now pass `conjVerb` (conjugated verb) instead of raw verb. `DbSeeder.cs` — 10 new subject-aware templates added across 5 categories using `{1}` (subject) and `{2}` (conjugated verb).

### B6 — Inference generalisation persists across turns (Medium)
`Core/ChatSession.cs:ProcessSentence` — Clear `InferredGeneralisation` context key at start of each call. Key no longer carries over to unrelated turns when the 50% display chance misses.

### B7 — SVO splits on gerund verbs (Medium)
`NLP/SvoExtractor.cs` — Skip triples where extracted subject is `"a"`, `"an"`, or `"the"`. Fixes triple corruption when `-ing` words are mis-tagged as verbs (e.g. "programming"→"a programming language").

### B8 — PendingSentimentFollowUp reads overwritten intensity (Medium)
`Core/ContextKeys.cs` + `ResponseEngine.cs` — New `PendingSentimentIntensity` context key stores the emotion intensity at the time `PendingSentimentFollowUp` is set. `GenerateResponse` reads from this key instead of `LastSentimentIntensity` (which gets overwritten by the next turn's `ProcessSentence`).

### B9 — Session summary uses un-conjugated verbs (Low)
`Knowledge/KnowledgeStore.cs:BuildSessionSummary` — New `FormatFact` helper applies `ResponseEngine.ConjugateVerb` to each fact's verb. `ConjugateVerb` made public.

### B10 — "Do you still feel that way?" for factual refs (Low)
`Data/DbSeeder.cs` — Changed `topic_reference_fact` template: "Do you still feel that way?" → "Is that still true?" (facts aren't feelings).

### B11 — Temporal confirmation uses future tense for past events (Low)
`Data/DbSeeder.cs` — Added 2 past-referencing `temporal_confirmation` templates: "I'll remember you mentioned that {0}." / "Noted — you said that {0}."

### Files modified
- `Core/ChatSession.cs` — B1, B2, B6
- `Core/ContextKeys.cs` — B8
- `Data/DbSeeder.cs` — B5, B10, B11
- `Knowledge/KnowledgeStore.cs` — B9
- `NLP/SvoExtractor.cs` — B7
- `Responses/ResponseEngine.cs` — B3, B4, B5, B8, B9

### Verify
- `dotnet build && dotnet test` — 223/223 pass

---

## Phase 24 — Random Short Story Generation ✅

A `StoryGenerator` engine that composes short stories from DB-stored templates, filling slots with random dictionary words and optionally weaving in the user's known facts. Triggerable explicitly ("tell me a story") or proactively (occasional `story_time` after the proactive question fallback).

### New files
- `Stories/StoryGenerator.cs` — core engine: picks template, resolves all `{slots}` via handler registry
- `Data/Entities/StoryTemplate.cs` — entity (Id, Template, Category?, CreatedAt)

### Slot system
| Slot | Source | Fallback |
|------|--------|----------|
| `{noun}` | Random `noun` from POS dict | hardcoded set |
| `{noun_plural}` | Pluralised `{noun}` via `Pluralise()` | same fallback pluralised |
| `{verb}` | Random `verb` from POS dict | hardcoded set |
| `{adj}` | Random `adjective` | hardcoded set |
| `{adverb}` | Random `adverb` | hardcoded set |
| `{place}` | Random noun from `noun_categories` where category="place" | hardcoded set |
| `{character}` | Random name from `users` table | hardcoded set |
| `{user}` | Current user's name | `"someone"` |
| `{user_like}` | Random object from user's Preference facts | Generic noun fallback |
| `{number}` | Random int 1–1000 | N/A |
| `{a_noun}` | `{noun}` with "a"/"an" prefix | same fallback |
| `{verb}ing` | Gerund form of `{verb}` (drops trailing 'e') | same logic |

### Modified files
- `Data/PokeChatDbContext.cs` — added `DbSet<StoryTemplate>`, fluent config with PK + required Template
- `Data/Schema.sql` — DDL for `story_templates` table
- `Knowledge/KnowledgeStore.cs` — added `GetRandomWord(type)`, `GetRandomNounByCategory(cat)`, `GetStoryTemplates()`, `GetRandomUserFact(userId)`, `GetRandomName()`
- `Data/DbSeeder.cs` — `SeedStoryTemplates()` with 10 templates; `story_response` (3) and `story_time` (3) bot response categories
- `Responses/ResponseEngine.cs` — added `StoryGenerator` field + constructor param (defaults to new instance), `HandleStoryRequest()` called before inference in `GenerateResponse`, proactive `story_time` at 1-in-6 chance
- `tests/PokeChat.Tests/Helpers/TestDataHelper.cs` — `SeedStoryTemplates()` with 3 templates, 2 story response bot entries

### New Tests (7 total, 230/230 pass)
- `StoryGenerator.GenerateStory_ReturnsNonEmptyString` — basic output
- `StoryGenerator.GenerateStory_ResolvesNounSlot` — `{noun}` not in output
- `StoryGenerator.GenerateStory_ResolvesUserSlot` — `{user}` not in output
- `StoryGenerator.GenerateStory_ResolvesUserLikeSlot` — `{user_like}` filled from user facts
- `StoryGenerator.GenerateStory_FallsBackWhenNoUserFacts` — no crash with no facts
- `StoryGenerator.GenerateStory_MultipleSlots_AllResolved` — no `{` remnants
- `ChatSessionTests.StoryRequest_ReturnsNonEmptyResponse` — integration test

### Migration
- `20260606075805_Phase24_StoryGeneration` — adds `StoryTemplates` table

---

## Phase 25 — Word Classification Follow-Up ✅

When the user teaches the bot a new word via clarification, the bot follows up with 1-2 brief classification questions to determine its type (person/place/thing/verb), enriching the POS dictionary and noun categories.

### Files modified
- `Core/ContextKeys.cs` — added `PendingClassificationWord`, `PendingClassificationCount`, `PendingPlaceWord`
- `Knowledge/KnowledgeStore.cs` — added `UpdateWordType(string word, string wordType)`
- `Core/ChatSession.cs` — modified `HandleClarification` to set pending classification, new `HandleClassification` and `HandlePlaceFollowUp` methods, wired into `ProcessInput`
- `Data/DbSeeder.cs` — 8 new bot response categories for word classification
- `tests/PokeChat.Tests/Helpers/TestDataHelper.cs` — matching seed data
- `tests/PokeChat.Tests/Core/ChatSessionTests.cs` — 7 new tests

### Key details
- **Limit:** Only asks classification for the first 2 unknown words per session (`PendingClassificationCount`)
- **Suggestion path:** If user affirms a spelling suggestion, classification is skipped (the word is already known)
- **Classification parsing:** Keyword matching (person/place/thing/verb/action/adjective/noun) + pattern matching ("it's a X", "it is a X")
- **Place follow-up:** If classified as "place", one extra turn asks "Have you ever been to {word}?" and stores a visit fact if affirmed
- **Fallback:** If classification can't be determined, word stays as "unknown" type — no further questions

### New tests (7)
1. `ProcessInput_UnknownWord_ClassificationFires_AfterLearn`
2. `ProcessInput_Classification_LearnsNoun`
3. `ProcessInput_Classification_LearnsVerb`
4. `ProcessInput_Classification_LearnsPlace_AsksFollowUp`
5. `ProcessInput_Classification_PlaceFollowUp_Yes_StoresVisit`
6. `ProcessInput_Classification_PlaceFollowUp_No_DoesNotStore`
7. `ProcessInput_Classification_Suggestion_DoesNotFire`

### Verify
- `dotnet build && dotnet test` — all pass

---



---

## Phase 26 — Chat Log & Session Improvements ✅

24 bugs from real tester chat logs plus session logging infrastructure.

### Issue Fixes

| # | Issue | Fix |
|---|-------|------|
| 1 | **CRITICAL: ResetAllUserData FK crash** | Added DELETE for ResponseFeedbacks, LearnedResponseRules, ConversationMetrics before Users |
| 2 | **Greeting during name prompt** → cold re-ask | HandleNameInput detects greeting tokens, returns friendly greeting + re-prompt |
| 3 | **"your you" grammar in follow-up** | ObjectPronouns skips `context_followup_with_object_self` for pronoun objects |
| 4 | **Rename patterns too narrow** | Added `"call you"`, `"rename you"`, `"rename yourself"`, `"change your name"`, `"i want to call you"` + guard against "your" false prefix |
| 5 | **"something" causes spellcheck interruption** | Added `"something"` (pronoun) to POS dict |
| 6 | **Identity questions get generic response** | Added identity-aware rules with `{BOTNAME}` replacement |
| 7 | **"what do you know about me" generic** | Added response rule |
| 8 | **Summary has garbage triples** | `SummaryFilters.IsGarbageFact` for interrogative subjects / question artifacts |
| 9 | **Correction handler exact match too strict** | Changed `==` to `Contains` for `"not what i meant"`, `"not helpful"`, `"that's better"` |
| 10 | **7 common words missing from POS** | Added `fun`, `use`, `now`, `said`, `solve`, `killed`, `idiot`, `once`, `meaning`, `met`, `grammar` |
| 11 | **ConjugateVerb doesn't skip modals** | ModalVerbs HashSet: can/could/will/would/shall/should/may/might/must |
| 12 | **"your {1}" creates ungrammatical output** | ObjectPronouns check skips prefix for pronoun/verb objects |
| 13 | **SpellChecker maxDistance=2 too permissive** | Reduced default to `maxDistance=1` |
| 14 | **Indiscriminate word learning on rejection** | HandleClarification returns early when suggestion is rejected |
| 15 | **Infinite context loop (~65 turns)** | TopicReferenceCount counter, breaks after 3 consecutive topic refs |
| 16 | **Insults met with greeting** | InsultPattern regex + sentiment check + `direct_insult` category (4 templates) |
| 17 | **"your in an office" grammar** | Changed templates from `"Tell me more about your {1}."` → `"Tell me more about {1}."` |
| 18 | **"a interesting" in story** | Added `{a_adj}` slot with `AddArticle`; updated 8 story templates |
| 19 | **Multi-operator math wrong partial answer** | `SimpleMath.Evaluate` returns null on trailing content after first binary op |
| 20 | **Reset missing trigger phrases** | Added `"start again"`, `"restart"`, `"lets start again"`, `"let's start again"` |
| 21 | **"ello" treated as name** | `IsCloseToGreeting()` Levenshtein check (dist ≤ 1) in ExtractName |
| 22 | **"im" (no apostrophe) not expanded** | Added `"im" → "i am"` + 12 other no-apostrophe contractions to seed |
| 23 | **"once" missing from POS dict** | Added to `pos_dictionary.json` as adverb |
| 24 | **"something" unknown → spellcheck** | Added `"something"` (pronoun) to `pos_dictionary.json` |

### Infrastructure
- `ResponseEngine.SetBotName()` + `_botName` for `{BOTNAME}` replacement in rule responses
- `SummaryFilters.IsGarbageFact` static filter

### Chat Session Logging
Per-session log files at `logs/session_{sessionId}_{timestamp}.log`:
- `SessionLogger` — basic/verbose modes, log rotation
- `SessionLogConfig` — JSON config loader
- `LogTurn` after each `GenerateResponse`, `LogSystem` for welcome/greeting/exit

### New Tests
- Tool layer: 15 tests (ToolRegistry 9, ResponseEngineToolTests 4, WebSearchTool 2)
- Chat logging: 13 tests (SessionLogger write/rotation, SessionLogConfig loading)

### Verify
- `dotnet build && dotnet test` — 267/267 pass

---

## Phase 27 — Built-in Tool Layer ✅

Lightweight tool system for response rules. Tools invoked via `{tool:name}` markers in rule response templates.

### New Files
- `Tools/ITool.cs` — `ITool` interface + `ToolResult` class
- `Tools/ToolRegistry.cs` — loads `tools/tools.json` config (disabled by default), `TryExecute(toolName, args)`, timeout via `CancellationTokenSource`, `${VAR}` env var resolution
- `Tools/BuiltIn/WebSearchTool.cs` — HTTP GET to DuckDuckGo API, returns AbstractText snippet or RelatedTopics fallback
- `Tools/BuiltIn/ReadUrlTool.cs` — HTTP GET, strips HTML tags, returns first 1000 chars
- `Tools/tools.json.example` — committed template without secrets

### Modified Files
- `Responses/ResponseEngine.cs` — `_toolRegistry`, `ProcessToolMarkers()`, `ToolMarkerRegex`. Replaces `{tool:name:args}` with tool output or fallback.
- `Core/ChatSession.cs` — creates `ToolRegistry`, passes to `ResponseEngine`
- `Data/DbSeeder.cs` — 2 response rules with `{tool:web_search}`; 4 tool response categories (7 entries)
- `.gitignore` — added `tools/tools.json`

### New Tests (15 total, 267/267 pass)
- `ToolRegistryTests` (9): disabled/unknown/enabled tool, config loading, empty query, bad URL
- `ResponseEngineToolTests` (4): no marker, no registry, disabled tool, unknown tool

---

## Phase 28 — Full MCP Protocol ✅

Upgraded built-in tool layer to Model Context Protocol (MCP) over stdio transport. Any MCP-compliant server process can register tools with the bot via `mcp.json`.

### New Files
- `MCP/McpModels.cs` — JSON-RPC 2.0 request/response models + MCP protocol types (`McpToolSchema`, `McpServerConfig`, `McpConfig`)
- `MCP/McpClient.cs` — stdio subprocess manager, sends `initialize` → `tools/list` → `tools/call` via JSON-RPC, timeout handling, crash recovery, auto-kill on dispose
- `MCP/McpToolAdapter.cs` — `ITool` adapter wrapping an MCP-discovered tool, delegates `Execute()` to `McpClient.ExecuteTool()`
- `MCP/McpRegistry.cs` — loads `mcp.json`, spawns enabled MCP server processes, discovers tools, merges into unified dictionary
- `mcp.json.example` — template with disabled `docs-search` server example

### Modified Files
- `Tools/ToolRegistry.cs` — optional `McpRegistry` parameter, `RegisterMcpTools()` merges discovered tools; `IsEnabled` returns true for tools without explicit config (MCP tools are pre-filtered by `mcp.json`)
- `Core/ChatSession.cs` — creates `McpRegistry`, passes to `ToolRegistry`; disposes `_mcpRegistry` in `Dispose()`
- `.gitignore` — added `mcp.json`

### New Tests (19 total, 286/286 pass)
- `McpRegistryTests` (6): missing/invalid/empty config, pre-built tools, disabled servers, dispose
- `McpToolAdapterTests` (4): name/description storage, no-connection failure, empty args, shared client
- `McpIntegrationTests` (6): mock MCP bash server — connect, discover tools, execute, before-connect failure, full registry flow, adapter full flow
- `ToolRegistryMcpIntegrationTests` (3): merged tools, MCP tool execution, no-registry regression

---

## Phase 29 — Optional LLM Support ✅

Ollama-backed optional LLM fallback when the bot exhausts all rule-based capabilities. The LLM is offered once per session; if accepted, all subsequent dead-end responses come from the LLM.

### New files
- `LLM/ILLMProvider.cs` — interface: `string? GenerateResponse(string input, string? userName)`
- `LLM/OllamaProvider.cs` — HTTP POST to Ollama's OpenAI-compatible endpoint, configurable model/timeout/URL, error returns null
- `LLM/LLMOrchestrator.cs` — wraps config + provider + state; per-session call counter; `IsAvailable`, `IsAccepted`, `UserDeclined` state management; implements `IDisposable`
- `tools/llm.json.example` — config template with `enabled: false`

### Modified files
- `Core/ContextKeys.cs` — added `PendingLLMOffer`, `LLMOriginalInput`
- `Data/DbSeeder.cs` — seeded `llm_offer` (2), `llm_thinking` (2), `llm_unavailable` (2), `llm_declined` (2) bot response categories
- `tests/PokeChat.Tests/Helpers/TestDataHelper.cs` — matching seed data
- `tests/PokeChat.Tests/LLM/StubLLMProvider.cs` — test stub for deterministic LLM response injection
- `Core/ChatSession.cs` — added optional `llmOrchestrator` constructor parameter; LLM flow: pending offer handler at top of `ProcessInput`, post-`GenerateResponse` check for `default_response` category to offer/use LLM; `GetLLMResponse()` with hardcoded fallback templates
- `Responses/ResponseEngine.cs` — added `IsDefaultCategory(string)` public static method
- `.gitignore` — added `tools/llm.json`

### New Tests (14 total, 300/300 pass)
- `LLMOrchestratorTests` (7): available/not-available, accept/decline, can-use, call-count limits, dispose
- `ChatSessionLLMTests` (7): pending offer yes/no/unavailable, accepted-LM used on subsequent fallback, declined not offered again, offer fires on default_response, no-LLM fallback

### Architecture
- **Config path:** `tools/llm.json`
- **Flow:** `ProcessInput` → pending offer handler (check for yes/no) → normal flow → `GenerateResponse` → if `default_response` category → offer (if available, not accepted, not declined) → prompt user → if accepted → LLM directly → learn from LLM response
- **Acceptance:** After acceptance, LLM is called directly on any future input that reaches `default_response`
- **Persistence:** One offer per session; declined = never offered again

### Verify
- `dotnet build && dotnet test` — 300/300 pass

---

## Phase 29b — Data-Driven MCP Tool Triggers ✅

Tool triggers defined in `mcp.json` instead of hardcoded in `DbSeeder`/`DatabaseInitializer`. Zero-code MCP tool addition.

### New files
- `MCP/McpToolTrigger.cs` — model for tool trigger config (Pattern, InputType, Responses)
- `MCP/McpAutoTriggers.cs` — generates catch-all triggers for tools without explicit config

### Modified files
- `MCP/McpModels.cs` — added `ToolTriggers` property to `McpServerConfig`
- `MCP/McpRegistry.cs` — stores server configs, `GetToolTriggers()` returns explicit + auto-generated triggers, `GetTriggerKeywords()` for POS auto-seeding
- `Responses/ResponseRules.cs` — added `MatchRule` overload accepting `List<ResponseRuleRecord>? toolTriggers`; matching priority: learned (conf≥7) > seeded+triggers (longest pattern wins) > learned (conf<7); tiebreaker on pattern length
- `Responses/ResponseEngine.cs` — accepts `List<ResponseRuleRecord>? toolTriggers` param, passes to `MatchRule`
- `Core/ChatSession.cs` — extracts tool triggers from `McpRegistry`, passes to `ResponseEngine`; `AutoSeedPosDictionary()` seeds tool name keywords into POS dict
- `Data/DbSeeder.cs` — removed 3 hardcoded mempalace response rules
- `Data/DatabaseInitializer.cs` — removed `SeedMempalaceRules()` and `SeedMempalaceDictionary()`
- `mcp.json.example` — added `toolTriggers` config section for mempalace server

### Key details
- **Config-driven:** Tool triggers live in `mcp.json` only — not persisted to DB
- **Catch-all fallback:** Servers without explicit triggers get `"(use|call|run|execute) (the )?(toolName) for (.+)"` auto-generated
- **POS auto-seeding:** Tool name segments added to POS dictionary on startup (e.g. `mempalace`, `search` from `mempalace_search`)
- **Edge cases:** disabled servers excluded, undiscovered tools handled by existing `tool_unavailable`, pattern-length tiebreaker prevents short-pattern wins

### Tests (13 new, 314/314 pass)
- `McpRegistryTests.GetToolTriggers_*` (4): no config, empty config, invalid JSON, explicit triggers
- `McpRegistryTests.GetToolTriggers_DisabledServer_Excludes` — no triggers from disabled servers
- `McpRegistryTests.GetTriggerKeywords_ReturnsToolNameSegments` — auto-seeding keywords
- `ResponseRulesTests.MatchRule_ToolTrigger_*` (4): matches before generic seeded, longest pattern wins, learned outranks, null triggers fallback
- `McpAutoTriggersTests` (3): valid trigger, input matches, special chars escaped

---

## Phase 30 — Enhanced LLM Integration ✅

AlwaysOn mode (`alwaysOn: true` in `llm.json`) removes the opt-in offer and call cap. 22 response categories enhanced via LLM prompt map. MCP tool results get LLM summarisation. Dictionary fallback uses LLM for definitions. Inference uses LLM for contradictions and generalisations. Story generation via LLM. Correction understanding via LLM reflection.

### New/modified files
- `LLM/LLMOrchestrator.cs` — added `AlwaysOn`, `SummariseToolResults`, `EnhancedCategories` to `LLMConfig`; `GenerateResponse` skips call cap when `AlwaysOn`
- `Responses/ResponseEngine.cs` — added `_llmGenerator` delegate, `_enhancedCategories` hashset, `BuildCategoryPrompt` map for 22 categories; modified `GetRandomResponse` to call LLM; `ProcessToolMarkers` passes tool results to LLM for summarisation; `HandleDictionaryQuery` uses LLM on unknown words; `HandleInferenceResponse` uses LLM for contradictions/generalisations; `HandleStoryRequest` uses LLM when available
- `Core/ChatSession.cs` — offer flow skips for AlwaysOn; dead-end fallback goes direct to LLM; `TryHandleCorrection` uses LLM reflection; `HandleDictionarySaveConfirmation` handles post-LLM save prompts; `AlwaysOnLLmAvailable` helper
- `Core/ContextKeys.cs` — added `PendingDictionarySave`
- `tools/llm.json.example` — added `alwaysOn`, `summariseToolResults`, `enhancedCategories`

### Config-controlled
- **`alwaysOn`:** when true, no offer/accept/up front — LLM used directly for dead-end categories and enhanced responses
- **`summariseToolResults`:** when true (default), MCP tool output summarised by LLM
- **`enhancedCategories`:** 17 categories in example config routed through LLM
- **`maxCallsPerSession`:** only applies when `alwaysOn=false`

### Tests (13 new, 327/327 pass)
- `LLMOrchestratorTests` (3): AlwaysOn available/ignores cap/respects decline
- `ResponseEngineTests` (3): enhanced categories use LLM when available, fallback to template when LLM unavailable, no enhancement when not in enhanced list
- `ChatSessionLLMTests` (5): AlwaysOn dead-end skip, AlwaysOn adds unknown word to dictionary, AlwaysOn learned pattern uses LLM, AlwaysOn correction uses LLM reflection, Non-AlwaysOn correction uses template fallback
- `ChatSessionTests` (1): AlwaysOn dead-end category uses LLM directly
- `KnowledgeStoreTests` (1): `SetDefinition` stores definition correctly

---

## Phase 31 — Clarification/Classification Cancel ✅

Added ability for user to cancel unknown word clarification and word classification flows mid-stream.

### Changes
- `ChatSession.cs`: added `IsClarificationCancelled()` check in `HandleClarification` and `HandleClassification`, `CancelClarification`/`CancelClassification` methods, `CancellationPhrases` HashSet (typo, never mind, forget it, my bad, etc.)
- `KnowledgeStore.cs`: added `RemoveLearnedWord`, `RemoveFromDictionary`

### Tests (3 new, 330/330 pass)
- `HandleClarification_Cancelled_ReturnsCancelMessage`
- `HandleClarification_CancelledDoesNotLearn`
- `HandleClassification_Cancelled_RemovesFromDictionary`

---

## Phase 32 — Word Game (Story Chain) ✅

Added a word game where the user and bot take turns adding one word at a time to build a story. LLM optionally participates as third player.

### Changes
- `ContextKeys.cs`: added `GameModeActive`, `GameStory`, `GameTurnCount`
- `ChatSession.cs`: added `TryHandleGameStart`, `HandleGameTurn`, `HandleGameEnd`, `PickGameWord` (POS-cycling), `GetGameResponse`; `GameStartPhrases`, `GameEndPhrases`, `GameStartWords` statics; hooks in `ProcessInput`
- `LLMOrchestrator.cs`: added `GenerateWordForGame` with strict single-word prompt
- `DbSeeder.cs` / `TestDataHelper.cs`: seeded `game_start`, `game_turn_prompt`, `game_stop`, `game_already_active` bot response categories

### Tests (5 new, 335/335 pass)
- `TryHandleGameStart_TriggersOnPhrase`
- `HandleGameTurn_UserSaysStop_EndsGame`
- `HandleGameTurn_BotAddsWord`
- `HandleGameTurn_UserSendsMultipleWords_TakesFirst`
- `TryHandleGameStart_AlreadyActive_ReturnsPrompt`
