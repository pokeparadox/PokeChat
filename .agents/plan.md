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

## Phase 4 — Low Priority (Polish)
Low-priority polish and minor improvements.

- [ ] `Program.cs` → `using var` (replace `try/finally` with `using var session = new ChatSession()`)
- [ ] Consolidate `Random` usage (use `Random.Shared` instead of instance `new Random()`)
- [ ] `Database.EnsureCreated()` → lazy/deferred (move out of constructor, call once at startup)
- [ ] Evaluate date storage format (ISO 8601 strings vs `DateTime` with value converters)
- [ ] `DbPath` resolution robustness (fallback to environment variable, graceful failure)

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

## Running the Plan

Before each phase, confirm `dotnet build` and `dotnet test` pass.

```bash
dotnet build   # must succeed
dotnet test    # must pass
```

Mark items `[x]` as completed. If a phase reveals additional issues, add them to the appropriate phase rather than blocking.
