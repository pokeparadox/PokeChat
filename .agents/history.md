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

## Maintenance & Cleanup (Post-Phase 11) ✅

Review-driven fixes applied alongside Phase 11.

- [x] **C1:** `ConjugateVerb` handles `was`/`were` (past tense verbs no longer corrupted to `"wases"`/`"weres"`)
- [x] **C2:** Exit commands reduced from 6 to 2 (`quit`, `exit` only); `bye`/`goodbye`/`see you`/`good night` now trigger farewell response rules instead of silent exit
- [x] **C3:** `dictionary_definition_saved` seed data now wired into `ChatSession.HandleDictionaryDefinition` via `KnowledgeStore.GetBotResponses()` (was using hardcoded list)
- [x] **D1:** Deleted unused `InMemoryDbFixture` (only `FreshDbContext` was referenced by tests)
- [x] **A2:** `POKECHAT_DB_PATH` environment variable overrides DB location
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

Review-driven fixes applied across multiple sessions.

- [x] **C1:** `ConjugateVerb` handles `was`/`were` (past tense verbs no longer corrupted to `"wases"`/`"weres"`)
- [x] **C2:** Exit commands reduced from 6 to 2 (`quit`, `exit` only); `bye`/`goodbye`/`see you`/`good night` now trigger farewell response rules instead of silent exit
- [x] **C3:** `dictionary_definition_saved` seed data now wired into `ChatSession.HandleDictionaryDefinition` via `KnowledgeStore.GetBotResponses()` (was using hardcoded list)
- [x] **D1:** Deleted unused `InMemoryDbFixture` (only `FreshDbContext` was referenced by tests)
- [x] **A2:** `POKECHAT_DB_PATH` environment variable overrides DB location
- [x] **CR1:** NounCategoriser eager `Save()` removed — callers own the save boundary
- [x] **CR2:** Duplicated path resolution (`ResolveDbPath`/`ResolveDataFilePath`) replaced with single `ResolveProjectRoot()`
- [x] **CR3:** Dead `ProperNoun` enum value removed from `PosTagger`
- [x] **CR4:** `GetResponsesForRule` N+1 query fixed with `.Include(r => r.Responses)`
- [x] **CR5:** `HandleNameInput` hardcoded greeting fallback replaced with DB-driven `greeting_words` lookup
- [x] **CR6:** `HandleClarification` redundant else-if collapsed into null-coalescing chain
- [x] **CR7:** Private `IsPunctuation` wrappers in `PosTagger`/`SpellChecker` replaced with direct `PunctuationHelper` calls
- [x] **CR8:** Test `SeedBotResponses` duplication extracted to shared `TestDataHelper`
- [x] **CR9:** Unused `Moq` dependency removed from test `.csproj`
- [x] **CR10:** Double-dispose pattern in `Dispose_DoesNotThrow` fixed

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

## Phase 16 — Temporal Knowledge ✅

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

## Phase 17 — Inference / Simple Reasoning ✅

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

## Phase 18 — Session Summarisation ✅

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

## Phase 19 — Self-Learning Response Patterns ✅

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

## Phase 20 — Multi-Turn Topic Tracking ✅

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

## Fix: Broken Conversation Flow (Post-Phase 20) ✅

Three-turn breakdown where clarification, question, and multi-verb sentences produce garbled context follow-ups.

### Changes
- **Remove dead empty `if` block** in `ChatSession.HandleClarification` (`ChatSession.cs:416-419`) — no-op code that did nothing
- **Set context after clarification** — `_context.UpdateLastSubject(_currentUserName)` + `_context.UpdateLastObject(pendingWord)` in `HandleClarification` so learned word becomes active topic
- **Filter garbage SVO triples** — `FunctionWords` HashSet (`"not"`, `"never"`, `"no"`) skips triples where `predicateType == PredicateType.General` and `resolvedObject` is a single function word. Prevents "(you, do, not)" from becoming last context, replacing "What else can you share about you and not?" with sensible follow-ups

### Files modified
- `Core/ChatSession.cs` — 3 changes (dead if removal, context after clarification, FunctionWords filter)

### Verify
- `dotnet build && dotnet test` — 207/207 pass

---

## Fix: Missing Contractions (Post-Phase 20) ✅

`"that's"` was not in the contractions table (45 seed entries covered pronoun+is but not demonstrative/WH-word+is). This caused `"That's nice!"` to tokenise to `["that's", "nice", "!"]` with `PosTag.Unknown`, which triggered the unknown-word handler before any SVO extraction could run.

### Changes
- **`NLP/SpellChecker.cs`** — Added `IsContractionOfKnownWord()`: dynamically detects unknown words matching contraction patterns (`'s`, `n't`, `'ll`, `'ve`, `'re`, `'m`, `'d`) where the root is a known dictionary word. Wired into `GetUnknownWords` so `"that's"`, `"who's"` etc. are never flagged as unknown. Works for all databases, existing and new, without requiring reseeding.
- **`Data/DbSeeder.cs`** — Added 9 missing `'s` contractions: `that's`, `there's`, `here's`, `what's`, `who's`, `where's`, `why's`, `how's`, `when's` (all → `"is"`). Total: 45→54.
- **`tests/PokeChat.Tests/Helpers/TestDataHelper.cs`** — Same 9 contractions added to test seed data.
- **`tests/PokeChat.Tests/NLP/SpellCheckerTests.cs`** — 7 new tests for `IsContractionOfKnownWord` + `GetUnknownWords` integration. Also fixed pre-existing broken test `IsPluralOfKnownWord_ReturnsFalse` (asserted `"cats"` was not a known plural, but `"cat"` is in the dictionary).

### Files modified
- `NLP/SpellChecker.cs`
- `Data/DbSeeder.cs`
- `tests/PokeChat.Tests/Helpers/TestDataHelper.cs`
- `tests/PokeChat.Tests/NLP/SpellCheckerTests.cs`
- `AGENTS.md` (contraction count 44→54, new Known Fixes entry)

### Verify
- `dotnet build && dotnet test` — 214/214 pass

---

## Fix: "ok" unknown + sentiment question ignored ✅

Two bugs: (1) `"ok"` missing from POS dictionary caused Levenshtein to suggest `"of"` instead. (2) After `emotion_followup` asked about a sentiment change, the answer was ignored — `HandleSentiment()` skipped mild emotions (intensity < 2), so the bot fell through to context follow-up: "Tell me more about Bob and fine."

### Changes
- **`Data/pos_dictionary.json`** — Added `{"Word": "ok", "Type": "adjective"}` after `"okay"`
- **`Core/ContextKeys.cs`** — Added `PendingSentimentFollowUp` constant
- **`Responses/ResponseEngine.cs`** — Two changes:
  - `GenerateResponse()`: between unknown word check and `HandleSentiment()`, checks `PendingSentimentFollowUp`. If set + intensity ≥ 1, returns sentiment-aware acknowledgement (positive/negative/fallback templates), clears flag.
  - `HandleSentiment()`: when `emotion_followup` fires (sentiment change detected), sets `PendingSentimentFollowUp = "true"`
- **`Data/DbSeeder.cs`** + **`tests/PokeChat.Tests/Helpers/TestDataHelper.cs`** — Seeded `sentiment_ack_positive` (×2), `sentiment_ack_negative` (×1), `sentiment_ack` (×1)
- **`tests/PokeChat.Tests/Core/ChatSessionTests.cs`** — Integration test: emotional → emotion_followup → sentiment acknowledgement, verify response is not context follow-up

### Files modified
- `Data/pos_dictionary.json`
- `Core/ContextKeys.cs`
- `Responses/ResponseEngine.cs`
- `Data/DbSeeder.cs`
- `tests/PokeChat.Tests/Helpers/TestDataHelper.cs`
- `tests/PokeChat.Tests/Core/ChatSessionTests.cs`

### Verify
- `dotnet build && dotnet test` — 215/215 pass

---

## Fix: SVO Auto-Learn Unknown Words + Missing POS Words ✅

After fixing "ok" and the sentiment flow, conversation testing revealed two blockers: (1) `"yes"`, `"yeah"`, `"yep"`, `"yup"`, `"nope"`, `"nah"` missing from POS dictionary caused them to be flagged as unknown during name confirmation. (2) Any noun like `"pizza"` or `"steak"` in an SVO position (e.g. "I love pizza") triggered the unknown-word handler before the sentiment/rule engine could run, breaking both sentiment detection and normal conversation flow.

### Changes
- **`Data/pos_dictionary.json`** — Added 7 missing words: `"yes"`(adverb), `"yeah"`(adverb), `"yep"`(adverb), `"yup"`(adverb), `"nope"`(adverb), `"nah"`(adverb), `"ok"`(adjective)
- **`Core/ChatSession.cs`** — Modified `ProcessSentence()` to extract SVO triples BEFORE setting unknown words on context, then auto-learn any unknown word that appears as a subject or object token within any valid triple (split by space). `AddToDictionary` + `AddLearnedWord` for each match; only set unknown words context for remaining words.
- **`tests/PokeChat.Tests/Helpers/TestDataHelper.cs`** — Added same 6 affirmation words to `SeedPosDictionary`

### New Tests (4)
- `ProcessInput_AutoLearnsUnknownWordInSvoObject` — "I love steak" → no unknown word response, fact stored
- `ProcessInput_AutoLearnsUnknownWordInSvoSubject` — "steak is tasty" → both unknown words auto-learned
- `ProcessInput_AutoLearnsUnknownWordInCompoundObject` — "I like pizza and steak" → "steak" auto-learned from compound object token match
- `ProcessInput_DoesNotAutoLearnUnknownWord_OutsideSvo` — "gobbledygook" → clarification still triggered

### Files modified
- `Data/pos_dictionary.json`
- `Core/ChatSession.cs`
- `tests/PokeChat.Tests/Helpers/TestDataHelper.cs`
- `tests/PokeChat.Tests/Core/ChatSessionTests.cs`

### Verify
- `dotnet build && dotnet test` — 219/219 pass
