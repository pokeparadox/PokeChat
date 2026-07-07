# Phase 52 — Coding: Error Knowledge Base

Seed and self-learn common compiler/interpreter errors with known fixes. When a tool returns an error, match it against the KB and suggest a fix.

## Design

- **Error matching:** parse tool output for known error patterns (`CS\d+`, `NU\d+`, `error:`, `FAILED`, exit code non-zero)
- **KB lookup:** `error_knowledge` table maps error code/regex → description → suggested fix
- **Self-learning:** when a user corrects a bot's suggestion, learn the new fix pattern (`LearnErrorFix`)
- **Context integration:** attach the last error to `CurrentError` context key, reference in follow-ups

## New entities

- `ErrorKnowledge` — `Id`, `ErrorPattern` (string, unique), `Description` (string), `FixText` (string), `Command` (string?), `Category` (string), `LearnedFromUserId` (int?, FK→users), `CreatedAt`

## Database changes

- New `error_knowledge` table
- EF Core migration: `AddErrorKnowledge`

## Modified files

- `Data/Entities/ErrorKnowledge.cs` — new entity
- `Data/PokeChatDbContext.cs` — `DbSet<ErrorKnowledge>`, fluent config (unique index on ErrorPattern)
- `Data/Schema.sql` — DDL
- `Data/DbSeeder.cs` — `SeedErrorKnowledge()` with ~60 common errors:
  - **C# / dotnet (25):** CS1003 (syntax), CS0246 (type not found), CS0103 (name doesn't exist), CS0117 (no definition), CS1501 (no overload), CS1061 (no definition), CS1729 (no constructor), CS0029 (cannot convert), CS0266 (cannot cast), CS7036 (no argument), NU1603 (version conflict), NU1107 (version conflict), NU1201 (incompatible), MSB3021 (copy error), MSB4018 (error)
  - **Git (10):** merge conflict, detached HEAD, not a git repo, no upstream, permission denied, divergent branches, rebase conflict, dirty working tree, detached HEAD, nothing to commit
  - **Test (5):** test failure, test timeout, no tests found, test runner crashed, test skipped
  - **Docker (5):** port already allocated, image not found, connection refused, daemon not running, build failed
- `Knowledge/KnowledgeStore.cs` — add `LookupError(string output)`, `LearnErrorFix(string pattern, string fix, int? userId)`, `GetLearnedErrorFixes()`
- `Core/ContextKeys.cs` — add `CurrentError`, `LastErrorFix`
- `Core/ChatSession.cs` — in coding persona, after MCP tool execution, parse output for errors → look up → if found, append fix suggestion to response; if user corrects, learn via `LearnErrorFix`
- `tests/PokeChat.Tests/Helpers/TestDataHelper.cs` — seed 10 representative errors for tests

## Error matching logic

```
LookupError(output):
  1. Check for CS/NU/MSB codes: regex `\b(CS|NU|MSB)\d+\b` → exact match in KB
  2. Check for "error:" patterns: full line containing "error:" → fuzzy match against ErrorPattern
  3. Check for common git error strings: "merge conflict", "detached HEAD" → match against pattern
  4. Return first match found, or null
```

## Key details

- Error lookup runs AFTER tool execution, before response generation
- If error found, append to response: "I see `CS1003`: syntax error. Check line {line} of `{CurrentFile}`."
- Self-learning: "that's not right, the fix is X" → `LearnErrorFix` stores new pattern
- New patterns checked against existing KB before storing (duplicate prevention)
- Line number extracted from error output via regex `line \d+` or `\((\d+),`
- No new tables for self-learning (uses existing `error_knowledge` table with `LearnedFromUserId`)
- **LLM fallback for unknown errors:** when `LookupError` returns null, the LLM offer fires → LLM analyses the error output → suggests a fix → bot stores the error+fix pair in `error_knowledge`. Next time the same error occurs, the non-LLM KB handles it.
- **LLM-to-KB pipeline:** LLM explanations are parsed into structured `(ErrorPattern, Description, FixText)` triples and stored alongside seeded entries. The KB is seeded with ~60 common errors but grows without bound via LLM-assisted learning.

## Tests (8 new)

1. `LookupError_FindsExactMatch`
2. `LookupError_FindsFuzzyMatch`
3. `LookupError_ReturnsNull_WhenNoMatch`
4. `LookupError_ExtractsLineNumber`
5. `LearnErrorFix_StoresNewPattern`
6. `LearnErrorFix_Duplicate_DoesNotStore`
7. `ErrorFixAppended_AfterToolError`
8. `UserCorrection_LearnsNewPattern`

## Verify

- `dotnet build` — succeeds
- `dotnet test` — all pass
