# Phase 53 — REST API Layer (LM Studio Compatible)

Expose PokeChat's engine as an HTTP REST API that mimics the LM Studio / OpenAI chat completions format. This allows tools like opencode, Continue.dev, or any OpenAI-compatible client to use PokeChat as an LLM backend — with PokeChat handling routine chat (zero energy) and routing novel/coding requests to the LLM fallback.

## Design

```
POST /v1/chat/completions      → OpenAI-compatible chat endpoint
GET  /v1/models                 → returns available models
GET  /v1/health                 → health check
```

Chat completions request:
```json
{
  "model": "pokechat",
  "messages": [
    {"role": "system", "content": "You are a helpful assistant."},
    {"role": "user", "content": "Hello!"},
    {"role": "assistant", "content": "Hi there!"},
    {"role": "user", "content": "What's 2+2?"}
  ],
  "max_tokens": 512,
  "temperature": 0.7,
  "stream": false
}
```

Response:
```json
{
  "id": "chatcmpl-xxx",
  "object": "chat.completion",
  "created": 1712345678,
  "model": "pokechat",
  "choices": [{
    "index": 0,
    "message": {"role": "assistant", "content": "4"},
    "finish_reason": "stop"
  }],
  "usage": {"prompt_tokens": 15, "completion_tokens": 1, "total_tokens": 16}
}
```

## New files

- `RestApi/Program.cs` — ASP.NET Core Minimal API entry point, starts Kestrel on configurable port
- `RestApi/ApiSessionManager.cs` — manages API sessions (maps session_id → userId, ContextTracker, persona)
- `RestApi/OpenAiModels.cs` — request/response DTOs matching OpenAI chat format
- `RestApi/appsettings.json` — Kestrel config, port (default 5000), session timeout
- `RestApi/PokeChat.RestApi.csproj` — separate project (or add ASP.NET Core packages to existing)

## Modified files

- `PokeChat.slnx` — add new RestApi project reference
- Option: add ASP.NET Core to existing `PokeChat.csproj` instead of a new project

## Session management

- Each API consumer gets a `session_id` (generated on first request, returned in response headers)
- `ApiSessionManager` stores in-memory: `Dictionary<string, SessionState>`
- `SessionState` holds: `UserId`, `ContextTracker`, `Persona`, `LastActiveTime`
- Sessions expire after 30 minutes of inactivity (background cleanup timer)
- Maximum 100 concurrent sessions (configurable)

## Smart routing logic

The REST API processes each request through the PokeChat engine in order:

1. **Extract last user message** from messages[] array
2. **Run through ChatEngine.ProcessInput()** (Phase 54 prerequisite — or use ChatSession directly if Phase 53 ships first)
3. **If engine returns a response** — return it (zero LLM energy)
4. **If engine returns no match** — route to LLMOrchestrator → Ollama
5. **Wrap result** in OpenAI format and return

## Streaming (future)

The initial implementation returns non-streaming responses only. Streaming support (`stream: true` with `text/event-stream` SSE) can be added as a follow-up. The non-streaming format covers ~95% of use cases including opencode integration.

## Config

```json
{
  "rest_api": {
    "enabled": true,
    "port": 5000,
    "sessionTimeoutMinutes": 30,
    "maxSessions": 100,
    "model": "pokechat"
  }
}
```

## Key details

- **No new DB tables** — sessions are in-memory only
- **First request sets user identity**: the first user message is checked for name patterns (same as console flow: "my name is X" → user lookup/create)
- **Streaming deferred**: SSE streaming adds complexity; ship non-streaming first
- **CORS**: Allow all origins by default (dev mode), configurable via `tools.json`
- **Security**: No auth by default (dev mode). Add optional `apiKey` validation in config.
- **Error handling**: Malformed requests return standard OpenAI error format: `{"error": {"message": "...", "type": "...", "code": 400}}`

## Tests (7 new)

1. `ChatCompletions_ReturnsValidFormat`
2. `ChatCompletions_ExtractsLastUserMessage`
3. `ChatCompletions_EmptyHistory_ReturnsError`
4. `Models_ReturnsModelList`
5. `Health_ReturnsOk`
6. `SessionManager_CreatesSession`
7. `SessionManager_ExpiresSession`

## Verify

- `dotnet build` — succeeds
- `dotnet test` — all pass
- `curl -X POST http://localhost:5000/v1/chat/completions -H "Content-Type: application/json" -d '{"model":"pokechat","messages":[{"role":"user","content":"hello"}]}'` — returns valid response

## Future

- Streaming support (server-sent events)
- Multi-model support (different PokeChat personas as different models)
- API key authentication
- Rate limiting
