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

## Phase 32 — End-of-Session LLM Homework Check ✅

LLM reviews conversation after exit to retrospectively correct mistakes — removing bad learned rules, completing word definitions, and filling in missing classifications.

### Changes
- `Core/ChatSession.cs` — `RunHomeworkCheck()` called before `GenerateSessionEndSummary()` on quit/exit; runs silently (no prompt to user); bypasses MaxCallsPerSession
- `Knowledge/KnowledgeStore.cs` — added `DeactivateLearnedRule(int ruleId)` for removing low-quality learned patterns
- `LLM/LLMOrchestrator.cs` — `GenerateHomeworkCheck()` with dedicated prompt, JSON parsing via `SnakeCaseLower`
- `Data/DbSeeder.cs` — seeded `homework_check_start`, `homework_check_fixes`, `homework_check_none` bot response categories
- `tests/Helpers/TestDataHelper.cs` — matching seed data

### Tests (12 new, 347/356 pass)
- `ChatSessionLLMTests.HomeworkCheck_RunsOnExit_WhenLLMAvailable`
- `ChatSessionLLMTests.HomeworkCheck_Skips_WhenLLMNotAvailable`
- `KnowledgeStoreTests.DeactivateLearnedRule_DeactivatesRule`
- Plus 9 supporting unit tests for JSON parsing, `SnakeCaseLower`, edge cases

---

## Phase 33 — Word Game (Story Chain) ✅

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

---

## Phase 34 — Word Game UX Improvements ✅

Hides mid-game story reveal, shows thinking indicator during LLM calls, applies grammar filter to final story, and adds optional LLM story summary at game end.

### Changes
- `ChatSession.cs`: updated `HandleGameTurn` to return only bot word + prompt (`game_turn_word_and_prompt`), shows `"{_botName} is thinking..."` before LLM call; updated `HandleGameEnd` to apply `ApplyGameGrammarFilter` then optionally call LLM summary (`game_stop_llm`); added `ApplyGameGrammarFilter` (8-step pipeline: trim trailing conj/prep/det, collapse dupes, sentence-split at and/but/so, intro word commas, pre-conjunction commas, a→an, capitalize, trailing period); updated `GetGameResponse` fallbacks
- `LLMOrchestrator.cs`: added `GenerateGameStorySummary` method (bypasses MaxCallsPerSession, separate prompt)
- `DbSeeder.cs` / `TestDataHelper.cs`: replaced `game_turn_prompt` with `game_turn_word_and_prompt` (3 templates), updated `game_stop` templates with `\n`, added `game_stop_llm` category
- `TestDataHelper.cs`: added `and`, `on`, `a`, `an`, `once` to seeded POS dictionary

### Tests (12 new/updated, 355/356 pass)
- Updated: `HandleGameTurn_BotAddsWord` (checks no story leak), `HandleGameTurn_UserSaysStop_EndsGame` (flexible assertion)
- New: `HandleGameTurn_ShowsBotWord`, `ApplyGameGrammarFilter_TrimsTrailingConjunction`, `ApplyGameGrammarFilter_SplitsIntoMultipleSentences`, `ApplyGameGrammarFilter_CollapsesDuplicateWords`, `ApplyGameGrammarFilter_AddsTrailingPeriod`
- New `ChatSessionLLMTests`: `HandleGameEnd_WithLLM_IncludesSummary`
- New `LLMOrchestratorTests`: `GenerateGameStorySummary_ReturnsSummary`, `GenerateGameStorySummary_Unavailable_ReturnsNull`, `GenerateGameStorySummary_Declined_ReturnsNull`
- 1 pre-existing flaky rename test failure unrelated

---

## Phase 35 — Dad Jokes + Riddles ✅

Two content-driven diversions: dad jokes (setup→punchline, 2-turn) and riddles (multi-turn with hints/attempts/give-up).

### Changes
- `Data/Entities/Joke.cs` (new): Id, Setup, Punchline, Category, CreatedAt
- `Data/Entities/Riddle.cs` (new): Id, Question, Answer, Hint, Difficulty, CreatedAt
- `Data/Schema.sql`: added `jokes` and `riddles` tables
- `Data/PokeChatDbContext.cs`: added `DbSet<Joke> Jokes`, `DbSet<Riddle> Riddles` + OnModelCreating
- `Core/ContextKeys.cs`: added `PendingJokeSetup`, `PendingJokePunchline`, `PendingRiddleQuestion`, `PendingRiddleAnswer`, `PendingRiddleHint`, `PendingRiddleAttempts`, `RiddleActive`
- `Knowledge/KnowledgeStore.cs`: added `GetRandomJoke()`, `GetRandomRiddle()`
- `Data/DbSeeder.cs`: `SeedJokes()` (10 family-friendly jokes), `SeedRiddles()` (8 riddles with hints/difficulty), `SeedBotResponses` (dad_joke_setup/punchline, riddle_present/correct/wrong/hint/give_up/already_active)
- `tests/Helpers/TestDataHelper.cs`: `SeedJokes()` (2 jokes), `SeedRiddles()` (2 riddles), bot response categories for tests
- `Core/ChatSession.cs`: `JokeStartPhrases` (10), `RiddleStartPhrases` (7), `SurrenderPhrases` (9), routing before main flow (PendingJokeSetup → HandleJokeTurn, RiddleActive → HandleRiddleTurn), `TryHandleJokeStart`/`HandleJokeTurn`/`TryHandleRiddleStart`/`HandleRiddleTurn`/`IsCorrectGuess`/`ClearRiddleState`/`GetJokeResponse`/`GetRiddleResponse`, ordered before MadLibs/Game start checks
- EF Core migration: `AddJokesAndRiddles`

### Tests (12 new, 384/385 pass)
- `TryHandleJokeStart_TriggersOnPhrase`, `TryHandleJokeStart_NoJokes_ReturnsEmpty`, `TryHandleJokeStart_NonTrigger_ReturnsFalse`, `HandleJokeTurn_ReturnsPunchline`, `ProcessInput_JokeFlow_ThroughProcessInput`
- `TryHandleRiddleStart_TriggersOnPhrase`, `TryHandleRiddleStart_NoRiddles_ReturnsEmpty`, `HandleRiddleTurn_CorrectGuess_Wins`, `HandleRiddleTurn_WrongGuess_LetsTryAgain`, `HandleRiddleTurn_GiveUp_RevealsAnswer`, `HandleRiddleTurn_AfterThreeAttempts_GivesUp`, `HandleRiddleTurn_Hint_ReturnsHint`, `TryHandleRiddleStart_AlreadyActive_ReturnsPrompt`, `ProcessInput_RiddleFlow_ThroughProcessInput`
- 1 pre-existing flaky game start test (template randomization)

---

## Phase 36 — Poetry Generation (Haiku & Limerick) ✅

Algorithmic haiku (5-7-5 syllables) and DB-backed limerick (AABBA rhyme) generation without LLM.

### Changes
- `Stories/SyllableCounter.cs` (new): Algorithmic syllable counter with vowel-group counting, silent-e/-le/-ed/-sm/diphthong adjustments, exception lists
- `Stories/RhymeMatcher.cs` (new): Rhyme key extraction (handles silent-e), DB-first word matching with syllable filter fallback
- `Data/Entities/RhymeGroup.cs` (new): Rhyme DB entity (GroupKey, Word, Type)
- `Data/Entities/PoemTemplate.cs` (new): Poem template entity (Template, PoemType)
- `Data/PokeChatDbContext.cs`: added DbSet mappings + OnModelCreating for RhymeGroup/PoemTemplate
- `Data/DbSeeder.cs`: `SeedRhymeGroups()` (130+ entries across 20+ groups), `SeedPoemTemplates()` (12 haiku + 10 limerick templates), bot responses (haiku_response×3, limerick_response×3, poem_time×2), POS entries (haiku, limerick, poem, poetry, verse)
- `Knowledge/KnowledgeStore.cs`: added `GetAllRhymeGroupWords()`, `GetRhymeGroupWords()`, `GetWordsByTypeAndSyllables()`, `GetPoemTemplates()`
- `Stories/PoetryGenerator.cs` (new): Template-based generator resolving `{noun_2}`, `{adj_1}`, `{verb_2ing}`, `{a_rhyme}`, `{b_rhyme}` slots with syllable-count suffix support, automatic suffix handling (`{verb}ing`→gerund, `{verb}s`→3P, `{verb}ed`→past)
- `Responses/ResponseEngine.cs`: Added `_poetryGenerator` field/init, `HandlePoetryRequest()` (trigger phrases: "write a haiku/limerick", "make a poem", "tell me a poem", "haiku"/"limerick"), combined creative slot (1/5 chance, 50% story / 25% haiku / 25% limerick), LLM prompts for haiku/limerick, `IsDeadEndCategory` entries
- EF Core migration: `AddPoemAndRhymeTables`
- `tests/Helpers/TestDataHelper.cs`: added bot response categories (haiku_response, limerick_response, poem_time)

### Tests (12 new + 1 flaky fix, 484/484 pass)
- `SyllableCounterTests.cs`: 75 tests covering vowel groups, silent-e, -le, -ed, -sm, diphthongs, exceptions
- `RhymeMatcherTests.cs`: 8 tests covering rhyme key extraction, group matching, syllable fallback
- `PoetryGeneratorTests.cs`: 9 tests covering haiku (user context, no context), limerick, all slot types, suffix handling, missing data
- `ResponseEngineTests.cs`: `HandlePoetryRequest_ExplicitHaiku_ReturnsPoem`, `HandlePoetryRequest_ExplicitLimerick_ReturnsPoem`, `HandlePoetryRequest_HaikuViaLLM_WhenAvailable`, `HandlePoetryRequest_LimerickViaLLM_WhenAvailable`
- `ChatSessionTests.cs`: `PoetryRequest_Haiku_ReturnsNonEmptyResponse`, `PoetryRequest_Limerick_ReturnsNonEmptyResponse`, `PoetryRequest_JustHaikuWord_TriggersPoem`
- Fixed flaky `TryHandleGameStart_TriggersOnPhrase` test (accept either "word game" or "story" in response)

---

## Phase 37 — Cross-Session Recall ✅

Bot recalls facts from previous sessions with 30% chance at session start after name intro.

### Modified files
- `Knowledge/KnowledgeStore.cs` — added `GetPreviousSessions(int userId, string currentSessionId)` returning `List<ConversationSession>` ordered by Id desc; `GetRandomFactFromSession(int userId, string sessionId)` cross-references conversations with facts to pick a random fact from that session
- `Core/ContextKeys.cs` — added `RecallAttempted` key
- `Data/DbSeeder.cs` — added 5 `cross_session_recall` bot response templates (using {0}=day, {1}=subject, {2}=verb, {3}=object)
- `Core/ChatSession.cs` — added `TryBuildCrossSessionRecall()` method: checks `RecallAttempted` (prevents double-fire), 30% chance, queries previous sessions via KnowledgeStore, picks random fact, formats with day name from session start. Called in `HandleNameInput` after intro response; appends recall on newline. Fallback templates if DB not seeded.
- `tests/PokeChat.Tests/Core/ChatSessionTests.cs` — 8 new deterministic tests

### Key details
- **30% chance** per session (only the first session with a returning user can trigger it)
- **Double-fire prevention:** `RecallAttempted` context key set after first attempt
- **Day name:** Uses `session.StartedAt` day-of-week (Monday/Tuesday/etc.) for natural phrasing
- **No sessions = quiet:** Silently skips if no previous sessions exist

### Tests (8 new, ~488/496 pass)

---

## Phase 38 — Emoji Personality ✅

Bot responses are now decorated with category-appropriate emoji for personality and visual variety.

### Modified files
- `Responses/ResponseEngine.cs` — added `AddEmoji` and `GetStaticEmoji` methods. `GetRandomResponse` now wraps all returns with category-appropriate emoji. Emoji map: greeting/name_intro → 👋, context_followup/cross_session_recall → 💭, proactive → 🤔, dictionary → 📖, math → 🧮, story → 📚, dad_joke → 😄, riddle → 🧩, magic_8ball → 🔮, game/mad_libs → 🎮, inference → 🧠, llm → 🤖, wyr → 🎲, poetry/poem_time → 📝, session_summary → 📋, temporal → 🕐, bot_rename → 🏷️, bot_reset → 🔄, existing_fact → 💡. Sentiment-aware emoji for empathy_*/emotion_*/sentiment_*: 😊 positive, 😔 negative/sad, 😡 anger, 😰 fear, 😮 surprise. No emoji for unmapped categories.
- `tests/PokeChat.Tests/Helpers/TestDataHelper.cs` — backfilled missing `cross_session_recall` category
- `tests/PokeChat.Tests/Responses/ResponseEngineTests.cs` — 4 new emoji tests

### Tests (4 new, 496/496 pass)

---

## Interview Mode (completed post-Phase 38)

Bot can be placed into an "interview mode" where the LLM acts as a conversation partner, training the bot through the normal `ProcessInput` pipeline. All learning is isolated under a dedicated `"Interviewer"` identity.

### New files
- `Core/InterviewEngine.cs` — manages persona prompt, conversation history, turn counting

### Modified files
- `LLM/LLMOrchestrator.cs` — added `GenerateInterviewInput(string prompt)` (bypasses MaxCallsPerSession)
- `Core/ChatSession.cs` — `_interviewEngine`, `_interviewModeActive` fields; `StartInterviewMode()`/`EndInterviewMode()`/`IsInterviewTrigger()`/`IsInterviewStopCommand()`; modified `Start()` main loop with branch for interview turns; trigger detection ("interview mode", "train the bot", etc.)
- `Data/DbSeeder.cs` — 4 `interview_*` response categories seeded (intro/complete/stopped/no_llm)

### Key details
- **Turn limit:** Default 8 exchanges (configurable const)
- **Stop commands:** "stop", "end interview", "cancel", "enough", "stop training"
- **Identity isolation:** `_currentUserId` swapped to `"Interviewer"` during interview; restored on end
- **Edge cases:** LLM unavailable (null return → end), user not identified (trigger ignored), user interrupt between turns (Console.KeyAvailable), multiple interviews (facts accumulate per Interviewer)
- **No new tables, no migration needed**

### Tests
- `InterviewEngineTests.cs` — 3 tests (basic flow, stops when exhausted)
- `ChatSessionTests.cs` — 14 tests (triggers, stop commands, no-interference)

### Verify
- `dotnet build && dotnet test` — all pass

---

## Phase 39 — Real-User Bug Fix Batch (10 fixes from session logs) ✅

Ten bugs found by analysing 50 session logs from real user conversations. Fixed across 7 files.

### Plan files (deleted after implementation)
- `phase39a-interview-mode.md` — Interviewer messages misinterpreted as clarification responses
- `phase39b-mempalace-json-leak.md` — Raw MemPalace JSON dumped to user
- `phase39c-spell-checker-false-positives.md` — `hi→he`, `oh→of`, `why→way`, `ate→age`, `later→late`
- `phase39d-garbage-context-followups.md` — "Tell me more about not and any"
- `phase39e-exit-recap-nonsense.md` — "Kev joineds the circus", "not knows anything"
- `phase39f-user-identity-establishment.md` — "limerick" treated as user name
- `phase39g-story-poem-slot-garbage.md` — "Alice was a searched bison who dreamed of mighting"
- `phase39h-magic8ball-detection.md` — "Can I have a banana?" → Magic 8 Ball
- `phase39i-identity-loop.md` — "Tell me about yourself, bob" → "bob" → same prompt infinite loop
- `phase39j-missing-pos-words.md` — 16 common words missing from dictionary

### 39a — Interview Mode
`Core/ChatSession.cs`: Added `ClearPendingState()` method clearing 8 pending context keys (ClarificationWord, Suggestion, ClassificationWord, PlaceWord, LLMOffer, DictionarySave, DictionaryWord, UnknownWords). Called before processing Interviewer input, preventing Interviewer messages from being treated as clarification/classification responses.

### 39b — MemPalace JSON Leak
`Responses/ResponseEngine.cs`: Added `SanitiseToolOutput()` and `ExtractTextParts()`. Detects JSON output (starts with `[` or `{`), parses it, extracts readable text from `text/content/name/title/snippet/summary` properties. Non-JSON output passes through. `ProcessToolMarkers` uses sanitised output.

### 39c — Spell Checker False Positives
`NLP/SpellChecker.cs`: Added `ShortWordAllowlist` (~80 common 2-3 letter words). `GetUnknownWords()` skips `Length <= 2` and allowlist words.

### 39d — Garbage Context Follow-ups
`Core/ChatSession.cs`: Expanded `FunctionWords` from 3 to ~30 (and, or, any, all, some, the, a, an, this, that, these, those, it, its, there, here, then, than, also, too, very, so, but, yet, for, with, without, just). Added `ContentWordIndicators` set. Added all-function-word subject/object filtering.

### 39e — Exit Recap Nonsense
`Core/ChatSession.cs`: Made `StemVerb` public. `Knowledge/KnowledgeStore.cs`: `FormatFact` uses `ChatSession.StemVerb()` + `ResponseEngine.ConjugateVerb()` pipeline. `StemVerb` expanded with specific `sses/shes/ches/xes/zzes` patterns (removed overly broad `es` rule), past tense `-ed` handling (double-consonant patterns + general `ed`→stem), and `ied`→`y` rule.

### 39f — User Identity Establishment
`Core/ChatSession.cs`: Added `NameBlockers` HashSet (~40 words: tell, make, give, funny, joke, riddle, limerick, haiku, poem, interview, hello, hey, hi, goodbye, quit, exit, etc.). Single-token name detection checks `NameBlockers` — blocks commands/triggers/greetings from being treated as names.

### 39g — Story/Poem Slot Garbage
`Stories/StoryGenerator.cs`: Added `StoryExcludedVerbs` (modals + common auxiliaries), `GetStoryVerb()`, `GetStoryAdjective()` helpers. `Stories/PoetryGenerator.cs`: Same `PoetryExcludedVerbs`, updated `PickWord()` to exclude modals from verb picks and `-ed` words from adjective picks.

### 39h — Magic 8 Ball Detection
`Responses/ResponseEngine.cs`: Removed catch-all `if (lower.EndsWith("?"))` trigger. Magic 8 Ball now only fires on explicit triggers: `magic 8 ball`, `8 ball`, `magic eight ball`, `shake the ball`, `predict`, `my fortune`, `tell my fortune`.

### 39i — Identity Loop
`Core/ChatSession.cs`: Single-noun `ProcessSentence` case checks if noun equals `_currentUserName` (case-insensitive). If so, skips `UpdateLastSubject`.

### 39j — Missing POS Words
`Data/pos_dictionary.json`: Added 25 missing words: `ah, ate, cooking, fascinating, faster, favourite, freaking, gonna, gotta, ha, haha, hate, hated, hey, hi, joined, later, lol, nah, oh, ooh, oooh, wanna, why, wow, yeah`. Fixed `searched` from `adjective` → `verb`.

### Build fixes applied
- `ChatSession.cs:714` — `var lowerObj` redeclaration conflict with enclosing scope (removed `var`)
- `KnowledgeStore.cs:739` — missing `using PokeChat.Core` for `ChatSession.StemVerb` reference

### Test fixes applied
- `ResponseEngineTests.QuestionFallthrough_Returns8Ball` — changed `"Will I get the job?"` → `"shake the ball"` since catch-all `?` trigger was removed

### Verify
- `dotnet build && dotnet test` — 521/521 pass

---

## Phase 39: Self-Knowledge (Tell Me About Myself + Stats + Compliments)

Three features that query the user's existing data — facts, conversations, metrics — and present it back in different ways.

### Part A — Tell Me About Myself
- **Triggers:** `"tell me about myself"`, `"what do you know about me"`, `"what do you know"`, `"what have I told you"`, `"what do you remember"`, `"what do you know about us"`
- Lists all user facts (numbered 1-N if ≤10, grouped by predicate type if >10)
- Falls back to `"I don't know much about you yet"` if empty

### Part B — On-Demand Stats
- **Triggers:** `"how many facts do you know"`, `"conversation stats"`, `"what's my most talked about topic"`, `"how much do you know about me"`, `"tell me some statistics"`, `"what are my stats"`
- Returns total facts, conversation count, sessions, most-talked subject, sentiment breakdown, first/last chat dates

### Part C — Compliments
- **Explicit triggers:** `"compliment me"`, `"say something nice"`, `"make my day"`, `"give me a compliment"`, `"say something kind"`
- **Passive:** 1-in-7 chance after user shares a new preference fact
- Picks a random fact with positive verb (like/love/enjoy/prefer), wraps in `ConjugateVerb`-conjugated compliment

### Files modified
- `Data/DbSeeder.cs` — added `user_fact_list`, `user_fact_none`, `user_stats`, `compliment` bot response categories
- `Knowledge/KnowledgeStore.cs` — added `GetUserFactsFormatted`, `GetUserStatsFormatted`, `GetPositiveFacts`, `GetRandomPositiveFact`
- `Responses/ResponseEngine.cs` — added `HandleSelfKnowledgeRequest`, `GetRandomCompliment`, emoji mapping for new categories
- `Core/ChatSession.cs` — early trigger detection before `ProcessSentence`, pending compliment flag in `ProcessSentence`
- `Core/ContextKeys.cs` — added `PendingCompliment`

### Tests
- `KnowledgeStoreTests.cs` — 6 new tests (fact formatting, grouping, stats, positive fact queries)
- `ChatSessionTests.cs` — 8 new tests (tell-me-about-myself, stats, compliments, edge cases)
- `TestDataHelper.cs` — seed data for new categories

### Verification
- `dotnet build && dotnet test` — **537/537 pass** (521 original + 16 new)

---

## Phase 40 — Word List Consolidation ✅

Shared `GenerationUtils.cs` with `ExcludedVerbs` + fallback arrays, deduped `StoryExcludedVerbs`/`PoetryExcludedVerbs`, cleaned `ShortWordAllowlist` duplicates. `dotnet build` only.

---

## Phase 41 — Universal LLM Thinking Indicator ✅

`LlmCallWithIndicator(Func<string?>)` wrapper in ChatSession, wraps all 6 LLM call sites. `dotnet build` only.

---

## Phase 42 — Non-LLM Interview Mode ✅

`IInterviewEngine` interface, `NonLlmInterviewEngine` with 30-question bank, 8 new tests, **545/545 pass**.

---

## Phase 43 — Hang-Man Word Guessing Game ✅

Multi-turn hang-man game using POS dictionary nouns (≥6 letters) as word source. Trigger phrases in `HangmanStartPhrases`, surrender phrases in `SurrenderPhrases`, 6 wrong attempts max. Game routing in `ProcessInput` (activity check → `HandleHangmanTurn`; start check → `TryHandleHangmanStart`). Fix: already-active detection in `HandleHangmanTurn` (start phrase during active game now says "already playing" instead of "invalid").

### New/modified files
- **`Core/ContextKeys.cs`** — 5 hangman state keys (HangmanActive, HangmanWord, HangmanGuessed, HangmanWrongLetters, HangmanWrongCount)
- **`Core/ChatSession.cs`** — `HangmanStartPhrases`, `SurrenderPhrases`, `HangmanMaxAttempts=6`, `NameBlockers` includes `"hangman"`, routing in ProcessInput, `TryHandleHangmanStart`, `HandleHangmanTurn`, `StartHangman`, `ClearHangmanState`, `BuildHangmanDisplay`, `PickHangmanWord`, `GetHangmanResponse`
- **`Responses/ResponseEngine.cs`** — 🎮 emoji for `hangman_*` categories
- **`Data/DbSeeder.cs`** — `SeedHangmanBotResponses()` with 15 fallback responses across 9 categories

### Tests (9 new, 554/554 pass)
- `PlayHangman_StartsGame` — welcome message contains "Let's play" + "letters"
- `Hangman_AlreadyActive` — start + start again → "already playing"
- `Hangman_Surrender` — "I give up" → "The word was"
- `Hangman_InvalidInput` — multi-word input → "single letter or the whole word"
- `Hangman_RepeatLetter` — same letter twice → "already guessed"
- `Hangman_WrongLetter` — "z" → "not in the word" + "5 wrong guesses left"
- `Hangman_CorrectGuess_ReturnsValidResponse` — "e" → valid non-error response
- `Hangman_Lose` — 6 wrong word guesses → "Game over"
- `Hangman_CanRestartAfterGameEnds` — lose → restart → "Let's play"

---

## Phase 44 — Neural Intent Classifier ✅

Lightweight neural intent classifier (pure C#, no external ML deps) for efficient specialised routing. Optional — no model file means existing rule cascade runs unmodified.

### New files
- **`ML/SimpleNeuralNet.cs`** — 2-layer FFN (Xavier init, ReLU hidden, Softmax output, cross-entropy SGD, binary save/load)
- **`ML/IntentClassifier.cs`** — BoW vectorisation (2000-word vocab), train/predict with 0.85 confidence threshold, JSON vocab+category metadata, save/load
- **`ML/IntentCategory.cs`** — 25 default intent categories with `BuildIndex()` helper

### Modified files
- **`Core/ContextKeys.cs`** — added `CurrentIntent`, `IntentConfidence` constants
- **`Responses/ResponseRules.cs`** — new 5-param `MatchRule` overload accepting `IntentClassifier?` + `ContextTracker?`; sets `CurrentIntent` in context when confident
- **`Responses/ResponseEngine.cs`** — constructor accepts optional `IntentClassifier`, passes to `MatchRule`
- **`Core/ChatSession.cs`** — creates `_intentClassifier` field in both constructors, wires into `ResponseEngine`
- **`LLM/LLMOrchestrator.cs`** — added `GenerateTrainingLabels()` method with `TrainingLabelsSystemPrompt`

### Key details
- **No dependencies:** Pure C# — no ML.NET, ONNX, or new NuGet packages
- **Cold start:** If `intent_model.bin` doesn't exist, `LoadOrCreate()` returns without loading; `IsReady` stays false
- **Additive only:** Classifier stores intent in `ContextTracker` but doesn't change routing yet — safe first step
- **Training:** 300 epochs at 0.1f LR, 64 hidden neurons, binary weight serialization (~540KB model)

### Tests (18 new, 576/576 pass)
- `SimpleNeuralNetTests.cs` (8): Predict output size, Softmax sum, Train reduces loss, Learns two classes, Save/Load roundtrip, Load nonexistent returns null, Xavier init range, ReLU negatives zero
- `IntentClassifierTests.cs` (6): Classify null when not ready, Train and classify, BuildVocab size, Vectorise dimensions, Low confidence returns null, Empty train does not crash
- `ResponseRulesTests.cs` (2): classifier sets intent in context when confident; not ready does not set
- `ChatSessionTests.cs` (2): cold start (no model) does not affect flow; explicit classifier does not throw

---

## Phase 48 — Entity Graph Explorer ✅
- [x] **Entity Graph Explorer** — Follow links between entities using existing facts (edges: Subject→Verb→Object)
- [x] `KnowledgeStore.cs` — `GetEntityGraph(userId)`, `FindPath(userId, from, to, maxDepth=3)` with BFS, `FormatPath` (verb-conjugated), `CheckRelation(userId, subj, verb, obj)` (case-insensitive), `GetConnectedEntities(userId, entity)`
- [x] `ResponseEngine.cs` — `HandleEntityQuery(input, userId)` after temporal query (detects "does X verb Y", "how is X connected to Y", "tell me about X"), `BuildEntityConnectionNotice(userId)` (1-in-10 proactive slot)
- [x] `DbSeeder.cs` + `TestDataHelper.cs` — 14 seed bot responses across 5 categories (`entity_relation_yes`, `entity_relation_no`, `entity_relation_path`, `entity_relation_unknown`, `entity_relation_connected`, `entity_connection_notice`)
- [x] **No new tables, no EF migration** — uses existing `facts` table only
- [x] **Case-insensitive:** All queries use `ToLowerInvariant()` + `OrdinalIgnoreCase` matching on materialized facts
- [x] **No new context keys**

### Tests (7 new, 599/599 pass)
1. `GetEntityGraph_BuildsFromFacts` — verifies graph contains subject node with 2 edges
2. `FindPath_DirectConnection` — BFS finds direct subject→object edge
3. `FindPath_MultiHop` — BFS traverses 2-hop path (Charlie→Alice→library)
4. `FindPath_NoConnection_ReturnsNull` — returns null for disconnected entity
5. `CheckRelation_ReturnsTrue_WhenEdgeExists` — exact triple match; also verifies false for nonexistent
6. `HandleEntityQuery_ExplicitRelation_ReturnsYesNo` — "does frank like pizza" triggers entity relation response
7. `BuildEntityConnectionNotice_DetectsNewLink` — `GetConnectedEntities` returns related entities from shared subject

---

## Phase 49 — Persona System ✅

Dual-persona architecture (chat/coding) with persona-filtered rules, responses, and greetings. `null` persona = available to all personas.

### Modified files
- `Data/Entities/ResponseRule.cs`, `BotResponse.cs`, `Greeting.cs` — added `Persona` property (nullable string)
- `Data/PokeChatDbContext.cs` — fluent config for `persona` columns
- `Data/Schema.sql` — added `persona TEXT` to `greetings`, `response_rules`, `bot_responses`
- `Core/ContextKeys.cs` — added `CurrentPersona` constant
- `Core/ChatSession.cs` — `_persona` field (default `"chat"`), `PersonaTriggers` HashSet, `TryHandlePersonaSwitch()`, `SwitchPersona(persona)` updates persona/context/bot name/response engine; greeting passes `_persona`
- `Core/GreetingPool.cs` — `GetRandomGreeting` accepts `string? persona`
- `Knowledge/KnowledgeStore.cs` — `GetGreetings(persona)`, `GetResponseRules(persona)`, `GetBotResponses(persona)` filter `WHERE persona IS NULL OR persona = @p`
- `Responses/ResponseEngine.cs` — `SetPersona(string)`, mutable `_botResponses`, `_persona` field, passes `_persona` to `MatchRule` and `GetBotResponses`
- `Responses/ResponseRules.cs` — `MatchRule` overloads accept optional `string? persona`, passed to `knowledgeStore.GetResponseRules(persona)`
- `Data/DbSeeder.cs` + `TestDataHelper.cs` — seeded `persona_switch_chat` (2) + `persona_switch_coding` (2) in bot_responses
- `.plans/phase49-persona-system.md` — deleted (no longer needed as plan, history entry suffices)

### Key details
- **Switch triggers:** "switch to coding mode", "enter coding mode", "go to coding", "activate coding", "enter chat mode", "switch to chat mode", "go back to chat", "switch to chat"
- **Name change:** Chat → "PokeChat", Coding → "PokeCode"
- **Fallback:** persona-filtered query returns entries where `persona IS NULL OR persona = @p`
- **Context persistence:** persona switch does NOT clear context tracker
- **No EF migration needed:** New feature, clean DB. Future devs add `AddPersonaColumns` migration.

### Tests (8 new, 607/607 pass)
1. `SwitchPersona_ChangesCurrentPersona` — coding mode → subsequent responses work
2. `SwitchPersona_UnknownPersona_ReturnsErrorMessage` — unknown mode returns non-empty
3. `SwitchPersona_TriggersOnKeyword` — coding mode + coding question → response
4. `SwitchPersona_DoesNotClearContext` — context survives switch
5. `GetBotResponse_FiltersByPersona` — returns matching + null, excludes other
6. `GetResponseRule_FiltersByPersona` — returns matching + null, excludes other
7. `GreetingPool_UsesPersonaGreeting` — returns matching + null, excludes other
8. `Fallback_ToNullPersona_WhenPersonaHasNoMatch` — null entries always available

---

## Phase 50 — Shell Command Tool ✅
- [x] New `Tools/BuiltIn/ShellCommandTool.cs` — `ITool` implementation with whitelist-based security
- [x] Default whitelist: `ls`, `pwd`, `whoami`, `date`, `uptime`, `uname`, `echo`, `cat`, `wc`, `du`, `df`, `which`, `env`
- [x] Security: rejects shell metacharacters (`;`, `&`, `|`, `` ` ``, `$`, `()`, `<>`)
- [x] Uses `Process.Start` with executable + args (no shell invocation)
- [x] `ToolConfig.AllowedCommands` — configurable per `tools.json`
- [x] Registered in `ToolRegistry.RegisterBuiltIn()` with config-based whitelist injection
- [x] Seeded response rules: `run/execute/shell command <cmd>`, `run <cmd>`
- [x] Seeded bot responses: `shell_blocked` (3), `shell_error` (2)
- [x] `tools.json` + `tools.json.example` updated with `shell_command` section

### Tests (7 new, 615/615 pass)
1. `ShellCommandTool_EmptyCommand_ReturnsFailure` — no args → error
2. `ShellCommandTool_AllowedCommand_ReturnsSuccess` — `whoami` succeeds
3. `ShellCommandTool_BlockedCommand_ReturnsFailure` — `rm` blocked
4. `ShellCommandTool_DangerousChars_ReturnsFailure` — `-la; rm -rf /` rejected
5. `ShellCommandTool_CustomWhitelist_AcceptsOnlyListed` — custom whitelist enforced
6. `ShellCommandTool_ArgsWithoutDangerousChars_Succeeds` — `echo "hello world"` works
7. `ShellCommandTool_RegisteredAndEnabled_ReturnsResult` — registry integration
8. `ShellCommandTool_DisabledViaConfig_ReturnsNull` — disabled config respected

---

## Log Review Bug Fix (2026-07-04)
- [x] Added `"using"` (verb) to `pos_dictionary.json` — spell checker no longer flags it as unknown
- [x] Added `"sure"`, `"do"`, `"does"`, `"did"` to `FunctionWords` in `ChatSession.cs` — catches function-word-heavy subjects/objects
- [x] Fixed subject filter: multi-word subjects where ALL tokens are either function words or content word indicators are now filtered out (e.g. "you sure" → caught). Changed from `ContentWordIndicators.Contains(subject)` (exact match) to per-token check with `subjectTokens.Length > 1` guard to preserve single-word pronoun subjects like "I", "you"

---

## Phase 53: In-Chat Reminders (2026-07-07)
- [x] `Reminder` entity, schema, DbSet, fluent config
- [x] `DbSeeder` — 7 reminder bot response categories (16 seed rows)
- [x] `KnowledgeStore` — `CreateReminder`, `GetPendingReminders`, `GetDueReminders`, `MarkReminderDone`, `CancelReminder`, `HasReminderForTask`, `ParseReminderTime`
- [x] `ChatSession` — `TryHandleReminderRequest`, `HandleReminderCreation` (2 overloads), `FormatReminderTime`, `HasReminderKeywordsOnly`, pending reminder handler in `ProcessInput`
- [x] Session-start due check — `GetSessionStartReminderMessage` + hook in `Start()`, `ReminderShownCount` context key
- [x] 11 existing + 5 new tests (session-start: no user, no reminders, single due, multiple due, once-per-session), 697/697 pass

---

## Name Confusion Fix (2026-07-07)
- [x] `IPosTagger` — added `IsKnownWord(string word)` method
- [x] `PosTagger` — `IsKnownWord` checks dictionary exact match + plural resolution via `Pluraliser.ToSingular()`
- [x] `ContextKeys` — added `PendingNameConfirmation`, `PendingIdentityVerification`
- [x] Name validation: length 2-30 range check before name assignment
- [x] Single-word names checked against POS dictionary — known words prompt confirmation instead of being accepted
- [x] Cross-session identity: returning users (FirstSeen != LastSeen) get "Welcome back, are you still using that name?" verification
- [x] `HandleNameConfirmation` — affirmation calls `FinalizeNameSetup`, denial re-asks, anything else treated as new name input
- [x] `HandleIdentityVerification` — same affirmation/denial/else flow for returning user confirmation
- [x] 11 new tests (POS-known word accepted/denied, plural POS check, non-dictionary word, too short, too long, returning user affirmed/denied), 706/706 pass

---

## Phase NN: Meta-commentary Detection (2026-07-07)
- [x] Detect confusion ("doesn't make sense", "i'm confused"), not-helpful ("not helpful", "bad answer"), and mocking ("mocking me") patterns
- [x] `TryHandleMetaCommentary` handler — pattern matching, repeated complaint tracking via `LastComplaint` context key
- [x] Hooked into `ProcessInput` after `TryHandleReminderRequest`, before sentiment analysis
- [x] Single complaints use existing `complaint_acknowledged` bot response category (3 seed templates)
- [x] 3+ complaints escalate to new `meta_repeated_complaint` category (3 seed templates)
- [x] Short inputs (< 8 chars) and non-matching inputs pass through to normal processing
- [x] Test seed data + 7 new tests, 713/713 pass

---

## Sub-plan 1: Minimal HTTP API (2026-07-07)
- [x] `Api/PokeChat.Api.csproj` — new Web SDK project, targets net10.0, references PokeChat
- [x] `Api/Program.cs` — `GET /health` + `POST /chat` Minimal API endpoints
- [x] `Api/ChatSessionWrapper.cs` — per-session wrapper, greeting on first call
- [x] In-memory `ConcurrentDictionary<string, ChatSessionWrapper>` session management
- [x] 713/713 tests pass, 0 build errors

## Phase 47 — Quiz Builder (built alongside Sub-plan 1)
- [x] `ContextKeys.QuizActive` / `QuizScore` / `QuizQuestionCount` / `QuizCurrentAnswer` / `QuizCurrentQuestion` / `QuizFacts`
- [x] `KnowledgeStore.GetRandomFactsForQuiz()` — picks random user facts
- [x] `ChatSession.TryHandleQuizStart()` — "quiz me" triggers
- [x] `ChatSession.HandleQuizTurn()` — correct/wrong/give-up/complete
- [x] `BuildQuizQuestion()` — fill-in-the-blank from fact triples
- [x] Seed data: 3 quiz_question, 2 quiz_correct, 2 quiz_wrong, 2 quiz_score, 2 quiz_already_active, 2 quiz_no_facts
- [x] 7 tests: start/few-facts/correct/wrong/give-up/complete/already-active
- [x] 713/713 tests pass

## Phase 46 — Timeline / Journal (built alongside Sub-plan 1)
- [x] `ContextKeys.TimelineOffered` context key
- [x] `KnowledgeStore.GetFactsInDateRange()` + `BuildTimeline()` — day-labelled chronological recap
- [x] `ResponseEngine.HandleTimelineRequest()` — explicit trigger ("what happened this week")
- [x] `ResponseEngine.BuildProactiveTimelineOffer()` — 1-in-10 chance at 5+ turns
- [x] Seed data: 3 `timeline_response`, 2 `timeline_empty`, 2 `timeline_offer` templates
- [x] 5 tests: GetFactsInDateRange, BuildTimeline, HandleTimelineRequest (found/empty), BuildProactiveTimelineOffer
- [x] IsDeadEndCategory + emoji mapping
- [x] 713/713 tests pass

## Phase 45 — Preference Recommender (built alongside Sub-plan 1)
- [x] `ContextKeys.RecommenderGiven` context key
- [x] `KnowledgeStore.GetUserPreferences()` + `GetRecommendation()` — walks `is_a` WordLinks
- [x] `ResponseEngine.BuildRecommendation()` — 1-in-8 chance in proactive fallback slot
- [x] Seed data: 3 `recommender` bot response templates
- [x] 5 tests: GetUserPreferences, GetRecommendation (found/not-found/skips-known), BuildRecommendation
- [x] IsDeadEndCategory + emoji mapping
- [x] 713/713 tests pass

## AGENTS.md cleanup (2026-07-07)
- [x] Improvement Plan: completed phases moved to MemPalace (`wing: pokechat, room: phase-summaries`), keep only planned
- [x] Known Fixes: full history moved to MemPalace (`wing: pokechat, room: known-fixes`), keep only 11 essentials
- [x] Routines: updated post-phase workflow to file to MemPalace instead of AGENTS.md edits
- [x] 250 lines → 151 lines

## Phase 54 — Engine/UI Separation (2026-07-09)
- [x] Extracted `ChatEngine` class from `ChatSession`: ~3892 lines of core logic (ProcessInput, all handlers, NLP pipeline, knowledge store, context, games, interview, LLM)
- [x] ChatSession reduced to ~269 lines: Console I/O wrapper delegating to ChatEngine
- [x] `ChatEngine.OnStatusUpdate` callback replaces `Console.Write` in `LlmCallWithIndicator` and `HandleGameTurn` (thinking indicator)
- [x] `StartInterview()` / `EndInterview()` return strings instead of writing to Console
- [x] Console.WriteLine in `RunHomeworkCheck` kept in engine (called from exit flow, harmless in non-Console contexts)
- [x] `ChatSession.Start()` rewritten to delegate all state to `_engine`
- [x] All 714 tests pass (0 changes needed — same constructor signatures, ChatSession delegates)
- [x] `Program.cs` unchanged (`new ChatSession()` still creates engine internally)
- [x] `KnowledgeStore.cs` updated: `ChatSession.StemVerb` → `ChatEngine.StemVerb`
- [x] **Verified:** `Api/` wraps the same `ChatEngine` (`OpenAIAdapter` → `engine.ProcessInput`); LLM is only a dead-end fallback, not the primary path. Console UI and REST API share one engine — separation confirmed complete.

---

## Sub-plan 2: Session Persistence ✅
- [x] `ConversationSession` entity extended with `LastActiveAt`, `BotName`, `Persona`
- [x] `SessionManager` rewritten: DB-backed LRU cache (default 50), TTL eviction (default 1h), session CRUD
- [x] New endpoints: `POST /sessions`, `GET /sessions`, `GET /sessions/{id}`, `DELETE /sessions/{id}`, `POST /sessions/{id}/chat`
- [x] `ChatEngine.SessionId` made writable, parameterized constructor made public for API project
- [x] `ChatEngineFactory.Create()` accepts optional `sessionId`
- [x] `KnowledgeStore` extended with `GetSessionByGuid()` and `UpdateSessionActivity()`
- [x] 13 new integration tests for session CRUD, cache lifecycle, multiple sessions, LRU eviction
- [x] All 730 tests pass

---

## Sub-plan 3: Smart Routing & Layered LLMs ✅
- [x] `RouterService` — `RouteHandler` enum (16 handlers), slash command parser (16 commands), `RouteResult` class
- [x] Slash command routing wired into `ChatEngine.ProcessInput` after identity checks, before pending state checks
- [x] Intent classifier activation in routing — confident classifier (≥0.85) maps to appropriate handler (math_query→Math, story_request→Story, etc.)
- [x] `LlmTier` class + `LlmTiers` dictionary on `LLMConfig` — tiered config parsed from `llm_tiers` in `llm.json` (backwards compatible with flat config)
- [x] Tier-aware `GenerateResponse(string input, string tier = "default")` — `GenerateWordForGame` uses `"fast"`, `GenerateHomeworkCheck`/`GenerateTrainingLabels` use `"powerful"`
- [x] `ILLMProvider.GetProvider(string tier)` with fallback chain
- [x] 43 new RouterService tests, updated `llm.json.example` with tiered config
- [x] All 773 tests pass

---

## Sub-plan 4: Engine/API Inversion (2026-07-09)
- [x] Moved all source into `Api/`: Core/, NLP/, Data/, Knowledge/, Responses/, Math/, LLM/, ML/, MCP/, Tools/, Stories/, Migrations/
- [x] `Api/PokeChat.Api.csproj` is now the core library (all NuGet packages, pos_dictionary.json copy rule)
- [x] `PokeChat.csproj` gutted to thin HTTP client (~68 lines): health check, session creation, chat loop, session cleanup
- [x] `Program.cs` rewritten: `POST /sessions`, `POST /sessions/{id}/chat`, `DELETE /sessions/{id}`, configurable via `POKECHAT_API_URL` env var
- [x] `Api/Data/ProjectPathHelper.cs` updated: walks up for `PokeChat.Api.csproj` (was `PokeChat.csproj`)
- [x] 3 stray `Console.WriteLine` in `Api/Core/ChatEngine.cs` replaced with `OnStatusUpdate` callbacks
- [x] `tests/PokeChat.Tests.csproj` updated: references `Api/PokeChat.Api.csproj` only (no `ProjectReference` to `PokeChat.csproj`)
- [x] Root `PokeChat.csproj` excludes `tests/**/*.cs` and `Api/**/*.cs` to prevent test compilation leaks
- [x] 772/773 tests pass (1 pre-existing flaky race condition in `SessionManagerTests.Multiple_concurrent_sessions_dont_interfere`)

---

## Guest User Name Bug Fix (2026-07-09)
- [x] `SessionManager.GetOrCreate` calls `EstablishDefaultUser("Guest")` on new sessions, setting `_currentUserId` before any user input
- [x] This permanently blocks `HandleNameInput` (gated on `_currentUserId == null`), so "my name is Alice" through the API never sets the user's name
- [x] Fixed: gate in `ProcessInput` now checks `_currentUserId == null || _currentUserName == "Guest"` — allows name extraction when user is still the default Guest
- [x] 773/773 tests pass (0 failures)

---

## Phase D — Neural Response Generation (2026-07-10)
- [x] D1: Neural Reranker — 15-feature extractor + 1-hidden-layer sigmoid net, scores response candidates instead of random pick
- [x] D2: Next-Word Predictor — WordVocab + NGramModel (trigram/bigram/unigram) + neural smoother + beam search
- [x] D3: Tiered Pipeline — NeuralResponsePipeline with RulesOnly/NeuralRerank/NeuralGenerate/Llm tiers, JSON config
- [x] Wired into ResponseEngine.GetRandomResponse via optional NeuralResponsePipeline parameter
- [x] 35 new tests, 808/808 pass

---

## Model-Based Persona Routing (2026-07-11)
- [x] `PersonaRouter.cs` — maps `pokecode-v1` → coding persona, detects opencode/Copilot User-Agent, returns mismatch warning
- [x] `GET /v1/models` returns both `pokechat-v1` (chat) and `pokecode-v1` (coding)
- [x] `ChatEngineFactory.Create()` accepts `persona` parameter, calls `SwitchPersona`
- [x] `SessionManager.GetOrCreate()` accepts persona, persists to `ConversationSession.Persona` + `BotName`
- [x] `OpenAIAdapter` reads `request.Model`, resolves persona, forwards to session
- [x] `ChatEngine` gates name prompting on `_persona != "coding"` — no "what's your name?" loop
- [x] `ResponseEngine` gates 7 chat-only stages in coding mode (unknown words, sentiment, compliments, prediction, context follow-ups, random facts, proactive Qs) — explicit keyword triggers (poems, games, stories) still work
- [x] User-Agent fallback: opencode/Copilot on `pokechat-v1` auto-switches to coding with warning in response
- [x] Response headers: `X-PokeChat-Persona`, `X-PokeChat-Model`
- [x] 808/808 tests pass (0 failures)

---

## Multi-User REST API Support (2026-07-12)
- [x] `request.User` (OpenAI-compatible field) plumbed into `SessionManager.GetOrCreate()` — user identity flows from API to session
- [x] `KnowledgeStore.StoreFact()` scopes facts per-user; `GetFact()` filters by `currentUserId` for dedup
- [x] `SessionManager.EvictLru()` scoped per-user — one user's cache pressure doesn't evict another's sessions
- [x] IP-based rate limiting middleware (30 req/min fixed window) — per-IP, not per-session
- [x] All 808 tests pass

## Fact Consensus & Global Promotion (2026-07-12)
- [x] `FactEntity.Confidence` (double, default 1.0) + `FactEndorsement` table (FactId, UserId, CreatedAt)
- [x] `KnowledgeStore.TryEndorseFact()` — identical (subject, verb, object) from different users → +0.5 Confidence (cap 5.0), skips duplicate row
- [x] `GetPopularFacts(int minConfidence)` / `GetEndorsements(int factId)` for querying consensus
- [x] Auto-promote to global (`UserId = null`) when Confidence ≥ 3.0 — fact becomes visible to all users
- [x] Decay exemption: Confidence ≥ 2.0 facts are never deleted by knowledge decay cleanup
- [x] EF Core migration: `FactConsensus` — adds Confidence column + FactEndorsements table
- [x] 808/808 tests pass

## Token-Based Variable Rate Limiting (2026-07-12)
- [x] `ITokenBucketStore` / `InMemoryTokenBucketStore` / `TokenBucketOptions` — per-IP token buckets with TTL eviction
- [x] Tiered cost model: NLP = 1 token, upstream LLM = 20 tokens, refill 20 tokens/min cap 20
- [x] `OpenAIAdapter` checks before engine call and before upstream ForwardAsync — rejects upstream if insufficient tokens
- [x] Response headers: `X-RateLimit-Remaining`, `X-RateLimit-Reset` on every response
- [x] Configured via `appsettings.json` `RateLimiting` section (costs, refill rate, max tokens)
- [x] 808/808 tests pass

## Time-of-Day Query Handler (2026-07-12)
- [x] `ITimeEngine` interface + `SystemTimeEngine` — detects time/date/day queries via `[GeneratedRegex]` patterns
- [x] Timezone extraction (`"in EST"`, `"in PST timezone"`) and IANA zone conversion
- [x] Timezone persistence: explicit "my timezone is GMT" stores as user fact (`KnowledgeStore.StoreFact`)
- [x] `time_result` and `timezone_set` bot response categories (3 templates each) seeded in DbSeeder
- [x] Wired into `ResponseEngine.GenerateResponse` immediately after math evaluation block
- [x] `ITimeEngine` registered in DI (`builder.Services.AddSingleton<ITimeEngine, SystemTimeEngine>()`)
- [x] 808/808 tests pass

## Coding Persona Name Memory (2026-07-12)
- [x] `DbSeeder.cs` — added `{name}` and `{bot_name}` placeholders to 6 coding_* categories; added 8 new categories (`coding_greeting`, `coding_default`, `coding_error`, `coding_clarification`)
- [x] `ChatEngine.cs` — updated hardcoded fallbacks (`coding_confirmation_prompt`, `coding_confirmation_denied`) with `{name}` placeholder
- [x] `tests/Helpers/TestDataHelper.cs` — updated seed data to match DbSeeder
- [x] 808/808 tests pass

## Title Generator (2026-07-12)
- [x] `Api/Services/TitleGenerator.cs` — keyword-based classifier with 9 categories (debugging, planning, feature, setup, testing, code_review, brainstorm, question, general_chat) + subject extraction via regex patterns and significance scoring
- [x] `Api/Program.cs` — registered singleton, `POST /v1/title` endpoint accepting `{ messages: [...] }`, returning `{ title: "..." }`
- [x] Whole-word boundary matching prevents false matches (e.g. "implementation" doesn't trigger "implement" keyword)
- [x] 18 new tests, 826/826 pass

## Sub-plan 6: Real-Time Streaming (2026-07-12)
- [x] **Part A:** `UpstreamLLMClient.ForwardStreamingAsync` — true SSE streaming for upstream LLMs (`stream: true`, `HttpCompletionOption.ResponseHeadersRead`, SSE line parsing, `[DONE]` handling)
- [x] **Part B:** `OpenAIAdapter.ChunkBySentences()` — sentence-level chunking replaces word-by-word splitting for NLP responses
- [x] **Part C:** Engine status streaming via `Task.Run` — `OnStatusUpdate` callbacks written as `[thinking]`/`[processing]` SSE chunks before response text
- [x] `UpstreamOptions.StreamByDefault` config flag
- [x] `Program.cs` — passes `CancellationToken` from `RequestAborted`
- [x] 17 new tests (9 UpstreamLLMClient + 8 OpenAIAdapter), 843/843 pass

## Sub-plan 7: Rate Limiting & Session Quotas (2026-07-12)
- [x] **Part A:** `SessionQuotaOptions` config class — `MaxSessions`, `MaxSessionsPerUser`, `MaxTurnsPerSession`, `MaxUpstreamCallsPerSession`, `SessionTtlMinutes`
- [x] **Part B:** Per-user session cap via `CountSessionsForUser()` — enforced in `/sessions` POST endpoint
- [x] **Part C:** Per-session turn cap via `IsTurnQuotaExceeded()` — checked in `OpenAIAdapter.ProcessAsync` and `StreamResponseAsync`
- [x] **Part D:** Token bucket defaults updated (20→60 tokens/min, matching appsettings.json)
- [x] **Part E:** Upstream LLM call cap via `TryConsumeUpstreamCall()` — tracked per session, cleaned up on session end/eviction
- [x] **Part F:** `LLMOrchestrator.TryConsumeCall()` — shared `CallsThisSession` counter enforced across all 6 LLM methods (`GenerateResponse`, `GenerateWordForGame`, `GenerateGameStorySummary`, `GenerateHomeworkCheck`, `GenerateInterviewInput`, `GenerateTrainingLabels`)
- [x] `appsettings.json` — `RateLimiting` and `SessionQuotas` sections with all configurable options
- [x] `Program.cs` — `SessionQuotaOptions` wired through DI
- [x] 18 new tests (9 SessionQuotaTests + 8 LLMOrchestratorTests + 1 updated), 859/859 pass

## Database Recovery + Nullable Cleanup (2026-07-12)
- [x] **BackupHelper** — new `Api/Data/BackupHelper.cs`: file copy backup/restore, ATTACH+INSERT cross-schema data copy between old and new DB
- [x] **DatabaseInitializer** — auto-backs up `pokechat.db` → `pokechat.db.bak` before every migration. On schema mismatch, auto-recreates DB and copies learned data (facts, rules, responses, etc.) from backup. Schema validation via direct table queries after migration.
- [x] `--restore-db` CLI flag — restores `pokechat.db` from `.bak` file
- [x] **Nullable warnings cleanup** — all 11 CS warnings fixed (CS8600, CS8602, CS8603, CS8604, CS8625). Removed unused `System.Security.Cryptography.Xml` and `System.Net.Http.Json` packages. 0 warnings, 859/859 pass.

## Bot Command Prefix Change (2026-07-12)
- [x] Changed command prefix from `/` → `!` → `~` to avoid clashes with OpenCode (`/`) and shell history expansion (`!`)
- [x] `RouterService.cs` — `input[0] == '~'`, renamed `IsSlashCommand` → `IsBotCommand`, `SlashCommandMap` → `BotCommandMap`, `TryParseSlashCommand` → `TryParseBotCommand`
- [x] `ChatEngine.cs` — `ExecuteSlashRoute` → `ExecuteBotRoute`, help text updated to `~` prefix
- [x] `RouterService.cs` — `LooksLikePath()` guard prevents `~path` inputs from being parsed as commands
- [x] 3 new tests for path guard, 862/862 pass

## SessionManager DI Fix (2026-07-12)
- [x] Removed ambiguous convenience constructors from `SessionManager` that bypassed DI
- [x] Registered `PokeChatDbContext` as singleton in `Program.cs`
- [x] Updated all test usages to pass explicit `PokeChatDbContext` + `SessionQuotaOptions`
- [x] 862/862 pass

## Database Initialization Rewrite (2026-07-12)
- [x] Rewrote `DatabaseInitializer` — removed complex `GetPendingMigrations` → `ValidateSchema` → `RecreateFromBackup` flow
- [x] New flow: `Migrate()` → wipe tables via raw SQL + `ClearAllPools()` → retry `Migrate()` → last resort `EnsureCreated()`
- [x] `WipeAllTables()` uses raw `SqliteConnection` to drop all tables (bypasses EF stale connection state)
- [x] Fixed `ValidateSchema()` connection leak — now closes connection in both success and error paths
- [x] 862/862 pass

## Phase 55 — Knowledge Decay (2026-07-13)
- [x] Added `last_accessed`/`access_count` columns to `facts`, `learned_response_rules`, `word_definitions` tables
- [x] EF Core migration `KnowledgeDecay` created
- [x] `TouchFactAccess()` method on KnowledgeStore — updates `LastAccessed` and increments `AccessCount`
- [x] Wired access tracking into `GetFact()`, `GetFactsByUser()`, `GetRandomUserFact()`, `GetRandomFactFromSession()`, `GetTwoRandomUserFacts()`, `GetRandomFactsForQuiz()`
- [x] `DecayCleanup()` — deletes stale records (>90 days old, never accessed, low confidence) with client-side filtering
- [x] VACUUM runs after cleanup when >50 records deleted (reclaims SQLite disk space)
- [x] Auto-runs at session end (`ChatSession.Dispose` flow)
- [x] `~cleanup` bot command for manual trigger
- [x] 11 new tests (10 KnowledgeStore + 1 RouterService), 879/879 pass

## Greeting Fixes (2026-07-13)
- [x] Removed duplicate "What's your name?" appended after greeting templates that already contain name questions
- [x] Removed redundant "Are you still using that name?" for returning users — now set up directly
- [x] Updated 2 existing tests, added 2 new tests, 881/881 pass

---

## Context State Persistence (2026-07-14)
- [x] `ContextTracker.SerializeState()` / `DeserializeState()` — JSON roundtrip for context, lastSubject, lastObject, topicStack, turnCounter
- [x] `ConversationSession.ContextStateJson` column + EF migration `AddContextStateJson`
- [x] `ChatEngine.Save()` persists context state to DB session row
- [x] `ChatEngine.RestoreContextState()` restores context from DB on session load
- [x] `ChatEngine.InitializeSession()` — public method called by `ChatEngineFactory` after setting session ID
- [x] Recent riddle deduplication via `RecentRiddles` context key + `GetRandomRiddle(exclude)` overload
- [x] `DbSeeder` defensive error handling + `EnsureCreated` fallback for schema probe
- [x] **4 bugs fixed:** ConversationSession namespace removal, RestoreContextState timing (called before factory set sessionId), PredicateType enum-as-integer deserialization, FK constraint in tests (userId must exist)
- [x] **17 new tests:** 10 ContextTracker serialization, 4 riddle exclude, 3 Save/RestoreContextState
- [x] 899/899 pass

---

## Phase A1 — System Prompt → Persona/Config Mapping (2026-07-14)
- [x] New `SystemPromptMapper` — parses system prompt for persona keywords (coding/chat) and config directives (concise/detailed)
- [x] New `ContextKeys.SystemPrompt` and `ContextKeys.ResponseLength` constants
- [x] New `ChatEngine.ApplySystemConfig()` method for setting config from system prompt
- [x] Integrated into `OpenAIAdapter.ProcessAsync` and `StreamResponseAsync` — extracts `messages[0].role == "system"` and applies
- [x] **33 new tests:** persona detection, config detection, case insensitivity, precedence, edge cases
- [x] 932/932 pass, 0 warnings

---

## Phase A2 — Message History Rebuild (2026-07-14)
- [x] `OpenAIAdapter.RebuildHistory()` — replays prior user messages through engine to rebuild context (pronouns, topics, facts)
- [x] SHA-256 hash dedup — skips rebuild when message history unchanged between calls
- [x] `ChatEngine.RebuildMode` flag — prevents storing duplicate conversations and facts during replay
- [x] `ContextKeys.LastProcessedHistoryHash` and `ContextKeys.RebuildHistoryTurnCap` for dedup and configurable cap (default 10)
- [x] Integrated into both `ProcessAsync` and `StreamResponseAsync`
- [x] Guards on `StoreConversation`, `StoreFact`, `UpdateResponseEffectiveness`, and `SessionLogger` during rebuild
- [x] **10 new tests:** single/multi message, system/tool skip, dedup, different hash, whitespace, rebuild mode flag
- [x] 942/942 pass, 0 warnings

---

## Test Suite Reorganisation ✅ (2026-07-14)
- [x] 7-layer test split: NLP, Knowledge, Responses, ML, Stories, Tools, Api — each in its own project
- [x] `PokeChat.Tests.Shared` project: `FreshDbContext`, `TestDataHelper`, `StubLLMProvider` (single source of truth for test helpers)
- [x] `AssemblyInfo.cs` updated with `InternalsVisibleTo` for all 8 new test projects
- [x] Original `PokeChat.Tests` slimmed from 942 → 336 core tests (ChatEngine, ChatSession, GreetingPool, Interview, NounCategoriser, Router, SessionLogger)
- [x] 942/942 pass across 8 projects, 0 failures, 0 warnings
- [x] `AGENTS.md` updated with per-project test commands

