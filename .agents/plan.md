# PokeChat — Improvement Plan

Phased plan ordered from highest to lowest priority. Each phase must build successfully before moving to the next.

---

## Phase 0 — Foundation
- [ ] Confirm `dotnet build` and `dotnet test` pass cleanly before any changes

---

## Phase 1 — Critical Bug Fixes

Bugs that produce incorrect behavior at runtime.

### 1.1 `KnowledgeStore.GetFact()` loads all facts into memory
- **File:** `Knowledge/KnowledgeStore.cs:42-48`
- **Problem:** `.Where()` is applied **after** `.SelectFacet<Fact>()`, so EF Core loads all rows client-side before filtering.
- **Fix:** Move the `.Where()` call before `.SelectFacet()`:
  ```csharp
  context.Facts.Where(f => f.Subject == subject && f.Verb == verb && f.Object == obj)
      .SelectFacet<Fact>().FirstOrDefault();
  ```
- **Verify:** `dotnet test`

### 1.2 Proper noun detection is dead code
- **Files:** `NLP/Tokenizer.cs`, `NLP/PosTagger.cs:88-95`
- **Problem:** `Tokenizer.Tokenize()` calls `input.ToLowerInvariant()`, so all tokens are lowercase by the time they reach `PosTagger`. `char.IsUpper()` in proper noun heuristic never matches.
- **Fix:** Either (a) preserve casing in the tokenizer and normalize only for dictionary lookups, or (b) remove the dead proper-noun branch. Option (b) is simpler and safe — proper nouns can be added via the POS dictionary instead.
- **Verify:** `dotnet test`

### 1.3 Abbreviation detection in `SentenceSplitter` broken
- **File:** `NLP/SentenceSplitter.cs:32`
- **Problem:** The extracted abbreviation token still contains the trailing period (e.g. `"dr."`) but is compared against period-free entries (`"dr"`). Never matches.
- **Fix:** Strip the period from `current` before the `is` pattern match:
  ```csharp
  var trimmed = current.ToString().TrimEnd('.').ToLowerInvariant();
  ```
- **Verify:** `dotnet test`

### 1.4 Pronoun resolution for "him"/"her" resolves to subject instead of object
- **File:** `Knowledge/ContextTracker.cs:35-44`
- **Problem:** `ResolvePronoun("him")` and `ResolvePronoun("her")` both return `_lastSubject`. Linguistically "him"/"her" (object pronouns) should resolve to `_lastObject`.
- **Fix:** Map `"him"` → `_lastObject`, `"her"` → `_lastObject` only as object pronoun (careful: "her" can also be possessive). Keep "he"/"she" → subject.
- **Verify:** `dotnet test`

### 1.5 Conversations stored with empty `BotResponse`
- **File:** `Core/ChatSession.cs:185`
- **Problem:** `ProcessSentence` stores the conversation with `string.Empty` as bot response because the actual response is generated later in the pipeline.
- **Fix:** Move the `StoreConversation` call to after response generation, or update the stored record with the actual response once known.
- **Verify:** `dotnet test`

---

## Phase 2 — High Priority

Architecture improvements and significant robustness issues.

### 2.1 Batch `SaveChanges()` calls in `KnowledgeStore`
- **File:** `Knowledge/KnowledgeStore.cs`
- **Problem:** Every mutation method calls `context.SaveChanges()` individually. The seeder calls many `Add*` methods in sequence, each triggering a separate transaction+roundtrip.
- **Fix:** Remove `SaveChanges` from individual methods. Expose a public `Save()` method. Callers (primarily `ChatSession` and `DbSeeder`) call `Save()` at logical boundaries.
- **Verify:** `dotnet build && dotnet test`

### 2.2 Eliminate global mutable static state in `PosTagger`
- **File:** `NLP/PosTagger.cs`
- **Problem:** `_wordTagMap` is static. Leaks state between tests, requires `Reset()`/`Initialize()` dance, not thread-safe.
- **Fix:** Convert to instance class with interface `IPosTagger`. Constructor takes `List<PosDictionaryEntry>`. Inject into `ChatSession` and test dependencies.
- **Verify:** `dotnet build && dotnet test`

### 2.3 Fix schema-entity mismatch for `Misspelling`
- **Files:** `Data/Entities/Misspelling.cs`, `Data/PokeChatDbContext.cs`, `Data/Schema.sql`
- **Problem:** Entity property is `WrongWord` but `Schema.sql` defines column as `misspelling`. EF Core will create `WrongWord` column, so schema doc is wrong. Also, the `ISpellChecker` interface (if introduced) and knowledge-store queries should be consistent.
- **Fix:** Either rename the entity property to `Misspelling` or add `.HasColumnName("misspelling")` in Fluent API. Update the doc/schema whichever aligns.
- **Verify:** `dotnet build`

### 2.4 Remove duplicate and useless POS entries
- **File:** `Data/DbSeeder.cs:1031-1032`
- **Problem:** `("express", "stop_word")` appears twice; `"stop_word"` entries are useless (map to `PosTag.Unknown`) and duplicate existing noun entries for `"express"`, `"info"`, `"others"`.
- **Fix:** Remove the three `stop_word` entries. They contribute nothing to tagging.
- **Verify:** `dotnet build && dotnet test`

### 2.5 `ClassifyPredicate` string literals → enum
- **File:** `Core/ChatSession.cs:212-247`
- **Problem:** Returns `"is_named"`, `"likes"`, `"is"` etc. as raw strings. Brittle, no discoverability, typos can silently create inconsistent data.
- **Fix:** Define `enum PredicateType { IsNamed, Likes, Is, Has, ... }`. Use it in `Fact` model and `SvoTriple` if appropriate.
- **Verify:** `dotnet build && dotnet test`

### 2.6 Context keys → constants
- **Files:** `Core/ChatSession.cs`, `Responses/ResponseEngine.cs`
- **Problem:** Magic strings like `"pending_clarification_word"`, `"unknown_words"`, `"last_response"`, `"pending_clarification_suggestion"` scattered across files.
- **Fix:** Define a `static class ContextKeys` with `public const string` fields. Reference throughout.
- **Verify:** `dotnet build`

### 2.7 `Microsoft.EntityFrameworkCore.Design` → `PrivateAssets=all`
- **File:** `PokeChat.csproj`
- **Problem:** Design-time-only package leaks as a runtime dependency.
- **Fix:** Add `<PrivateAssets>all</PrivateAssets>` and `<IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>`.
- **Verify:** `dotnet build`

---

## Phase 3 — Medium Priority

Code quality, maintainability, and testability improvements.

### 3.1 `PosTagger.Tag()` return type from `Dictionary` to list-based map
- **File:** `NLP/PosTagger.cs:50`
- **Problem:** `Dictionary<string, PosTag>` overwrites tags for duplicate tokens. `SvoExtractor` then reads wrong tag for repeated words.
- **Fix:** Return `List<(string Token, PosTag Tag)>` or `Dictionary<int, PosTag>` keyed by index.
- **Verify:** `dotnet test`

### 3.2 Consolidate duplicate `IsPunctuation`
- **Files:** `NLP/PosTagger.cs`, `NLP/SpellChecker.cs`
- **Problem:** Identical private method in two classes.
- **Fix:** Extract to a shared `NlpHelper.IsPunctuation(string)` static utility.
- **Verify:** `dotnet build`

### 3.3 Consolidate test DB helpers
- **Files:** `tests/PokeChat.Tests/Helpers/FreshDbContext.cs`, `tests/PokeChat.Tests/Helpers/InMemoryDbFixture.cs`
- **Problem:** Nearly identical classes.
- **Fix:** Keep one (e.g. `InMemoryDbFixture`), remove the other, update test references.
- **Verify:** `dotnet test`

### 3.4 Add interfaces to NLP classes for testability
- **Files:** `NLP/Tokenizer.cs`, `NLP/SentenceSplitter.cs`, `NLP/SvoExtractor.cs`, `Responses/ResponseRules.cs`
- **Problem:** All are static classes with no interfaces. Cannot be mocked. `ChatSession()` constructor hard-wires them.
- **Fix:** Define interfaces (`ITokenizer`, `ISentenceSplitter`, `ISvoExtractor`, `IResponseRules`). Convert classes to instance. Wire via constructor injection. Update `ChatSession` constructor (both production and test overloads).
- **Verify:** `dotnet build && dotnet test`

### 3.5 `IsStopWord` HashSet → static readonly field
- **File:** `Core/ChatSession.cs:346`
- **Problem:** `HashSet` allocated on every call to `IsStopWord`.
- **Fix:** Make it a `private static readonly HashSet<string>` initialized once.
- **Verify:** `dotnet build`

### 3.6 Fill test coverage gaps
- **Files:** `tests/PokeChat.Tests/` (new or expanded)
- **Missing:** Direct tests for `SpellChecker`, `ContextTracker`, `HandleClarification`, `LearnGreetingWords`, `ProcessSentence`. Integration tests for SVO extraction pipeline.
- **Verify:** `dotnet test` (coverage target: ≥70% on new code)

### 3.7 Extract POS dictionary to data file
- **File:** `Data/DbSeeder.cs` (~900 lines for POS alone)
- **Problem:** Massive hardcoded dictionary inflates the file and makes editing painful.
- **Fix:** Move POS entries (and misspellings) to `Data/SeedData/pos_dictionary.json` and `Data/SeedData/misspellings.json`. Load at seed time.
- **Verify:** `dotnet build && dotnet test`

### 3.8 ResponseEngine hardcoded strings → DB-driven
- **File:** `Responses/ResponseEngine.cs`
- **Problem:** Follow-up prompts and default responses are hardcoded strings, inconsistent with "learn from DB" philosophy.
- **Fix:** Add a `bot_messages` or `fallback_responses` table. Seed current hardcoded strings. Load at response time.
- **Verify:** `dotnet build && dotnet test`

---

## Phase 4 — Low Priority

Polish, idiomatic C#, and minor performance tweaks.

### 4.1 `Program.cs` → `using var`
- **File:** `Program.cs`
- **Fix:** Replace `try/finally` with `using var session = new ChatSession();`
- **Verify:** `dotnet build`

### 4.2 Consolidate `Random` usage
- **Files:** `Core/ChatSession.cs:314`, `Responses/ResponseEngine.cs:8`
- **Fix:** Use a shared `static readonly Random` (or `Random.Shared` in .NET 10) instead of instance `new Random()`.
- **Verify:** `dotnet build`

### 4.3 `Database.EnsureCreated()` → lazy/deferred
- **File:** `Data/PokeChatDbContext.cs:13`
- **Fix:** Move `EnsureCreated()` call out of constructor. Call once in `Program.cs` or `ChatSession` startup.
- **Verify:** `dotnet build && dotnet run`

### 4.4 Evaluate date storage format
- **Files:** All entity classes, `Data/Schema.sql`
- **Context:** Dates stored as ISO 8601 strings for SQLite compatibility. Evaluate if switching to `DateTime` properties with value converters is worth the SQL date-function support.
- **Verify:** `dotnet build && dotnet test`

### 4.5 `DbPath` resolution robustness
- **File:** `Data/PokeChatDbContext.cs:20-36`
- **Fix:** Fall back to a configurable path or environment variable. Fail gracefully with a clear message if `.csproj` not found.
- **Verify:** `dotnet build`

---

## Running the Plan

Each phase is self-contained. After completing a phase:
```bash
dotnet build
dotnet test
```

Mark items `[x]` as completed. If a fix reveals additional issues, add them to the appropriate phase rather than blocking.
