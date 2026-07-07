# Phase 54 — Engine/UI Separation

Extract the core processing logic from the monolithic `ChatSession` into a reusable `ChatEngine` class. The console `ChatSession` becomes a thin UI wrapper. The `ChatEngine` is consumed by both the console app and the REST API (Phase 53), with zero code duplication.

## Motivation

Currently `ChatSession` (~3200 lines) owns everything: NLP pipeline, knowledge store, game engines, LLM orchestrator, MCP registry, AND the console UI loop. This prevents the REST API from reusing the engine. The console and REST API share ~95% of code but have different I/O.

## Architecture

```
┌──────────────────────────────────┐
│          ChatEngine              │  ← new: stateless-ish processor
│  ┌────────────────────────────┐  │
│  │ Properties:                │  │
│  │  - CurrentUserId           │  │
│  │  - CurrentUserName         │  │
│  │  - BotName                 │  │
│  │  - Persona                 │  │
│  │  - SessionId               │  │
│  │                           │  │
│  │ Methods:                   │  │
│  │  - ProcessInput(string)    │  │
│  │  - ProcessSentence(...)    │  │
│  │  - TryHandleXxx(...)       │  │
│  │  - HandleXxx(...)          │  │
│  │  - Start() → greetings     │  │
│  └────────────────────────────┘  │
│                                  │
│  Owns:                           │
│  - _dbContext, _knowledgeStore   │
│  - _responseEngine               │
│  - _spellChecker, _posTagger     │
│  - _tokeniser, _svoExtractor    │
│  - _sentenceSplitter             │
│  - _context, _nounCategoriser   │
│  - _llmOrchestrator             │
│  - _mcpRegistry                 │
│  - _intentClassifier            │
│  - _interviewEngine             │
│  - _sessionLogger               │
└──────────┬───────────────────────┘
           │
    ┌──────┴──────┐
    ▼             ▼
ChatSession   RestApi
(console UI)  (REST API)
    │             │
    │ Start()     │ MapPost("/v1/chat/...")
    │ loop:       │ → engine.ProcessInput()
    │ ReadLine()  │ → format response
    │ WriteLine() │
    └─────────────┘
```

## New files

- `Core/ChatEngine.cs` — extracted from ChatSession, owns all state and processing logic
- `Core/ChatResponse.cs` — result type: `Text` (string), `Category` (string?), `UserId` (int?), `Persona` (string)

## Modified files

- `Core/ChatSession.cs` — gutted to a thin wrapper: creates `ChatEngine`, calls `engine.ProcessInput()`, handles console I/O, session lifecycle (Start → loop → exit)

## What moves into ChatEngine

All of these from `ChatSession`:

| Code | Destination |
|------|------------|
| All field declarations (NLP, Knowledge, LLM, MCP, games) | `ChatEngine` fields |
| Constructor (DB init, NLP init, seed data, tool loading) | `ChatEngine` constructor |
| `ProcessInput(string)` | `ChatEngine.ProcessInput()` |
| `ProcessSentence(...)` | `ChatEngine.ProcessSentence()` |
| All `TryHandle*` methods | `ChatEngine.*` |
| All `Handle*` methods (quiz, games, clarification, etc.) | `ChatEngine.*` |
| `SwitchPersona(string)` | `ChatEngine.SwitchPersona()` |
| `Dispose()` | `ChatEngine.Dispose()` |

## What stays in ChatSession

| Code | Reason |
|------|--------|
| `Console.ReadLine()` | Console-specific I/O |
| `Console.WriteLine()` | Console-specific output |
| Console colors / formatting | UI-specific |
| Greeting print at start | UI lifecycle |
| Exit message print | UI lifecycle |
| Exception handling / crash recovery | UI concern |
| `Start()` → console loop orchestration | UI lifecycle |

## ChatResponse type

```csharp
public class ChatResponse
{
    public string Text { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int? UserId { get; set; }
    public string Persona { get; set; } = "chat";
    public bool IsExit { get; set; }
    public string? SessionId { get; set; }
}
```

## Tests

The separation should be transparent — all existing tests already test through `ChatSession` (or directly through `ResponseEngine`/`KnowledgeStore`). No new tests needed. The refactoring passes if all existing 599+ tests pass unchanged.

However, add smoke tests:
1. `ChatEngine_ProcessInput_ReturnsResponse`
2. `ChatEngine_ProcessInput_EmptyInput_ReturnsEmpty`
3. `ChatEngine_SwitchPersona_ChangesResponseStyle`

## Key details

- **No behaviour change:** `ChatEngine.ProcessInput()` returns exactly the same string as `ChatSession.ProcessInput()`. The console output is identical.
- **No DB changes:** No new tables, no EF migration
- **Backward compatible:** `ChatSession` keeps its existing public API (`ProcessInput`, `Start`, `Dispose`). All existing tests pass without modification.
- **REST API ready:** After Phase 54, Phase 53's REST API creates a `ChatEngine` per session instead of a `ChatSession` per session.
- **Single-user per ChatEngine:** Each `ChatEngine` instance is single-user (owns one `ContextTracker`, one `CurrentUserId`). The REST API creates one `ChatEngine` per session.

## Verify

- `dotnet build` — succeeds
- `dotnet test` — all pass (same count)
- Console app runs identically to before
