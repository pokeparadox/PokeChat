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
- [ ] Evaluate date storage format (ISO 8601 strings vs `DateTime` with value converters)
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

## Running the Plan

Before each phase, confirm `dotnet build` and `dotnet test` pass.

```bash
dotnet build   # must succeed
dotnet test    # must pass
```

Mark items `[x]` as completed. If a phase reveals additional issues, add them to the appropriate phase rather than blocking.
