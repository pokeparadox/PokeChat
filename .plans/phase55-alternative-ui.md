# Phase 55 — Alternative UI (Godot / Web)

Build a standalone graphical frontend for PokeChat that talks to the REST API (Phase 53). The console is no longer the only face of PokeChat — any UI that speaks HTTP can be a client.

## Design

Two options (choose one or both):

### Option A: Godot UI (PokeChat Desktop)

- Godot 4 project in `/ui/godot/`
- Rich chat interface with text bubbles, typing indicators, avatars
- Inline rendering for special responses: quiz answers, story text, poetry (haiku/limerick formatting)
- Game integration: visual hang-man, mad libs slot UI, magic 8-ball animation
- Emoji support (PokeChat already adds emoji — render them natively)
- Settings panel: persona switching, bot name display, theme (dark/light)
- Cross-platform: Linux, Windows, macOS

Communication: HTTP client to `http://localhost:5000/v1/chat/completions`

### Option B: Web UI (PokeChat Web)

- Simple HTML/CSS/JS SPA in `/ui/web/`
- Served by the Phase 53 REST API as static files
- Zero build step — vanilla JS using `fetch()` to the API
- Progressive enhancement: keyboard shortcuts, auto-scroll, message history

Communication: Same API as Godot, but served from the same origin

## New files

- `ui/` — root for all UI projects
- `ui/godot/project.godot` — Godot project file
- `ui/godot/Scenes/` — MainMenu, ChatScene, GameScene
- `ui/godot/Scripts/` — ApiClient.gd, ChatManager.gd, GameRenderer.gd
- `ui/web/index.html` — single-page chat UI
- `ui/web/app.js` — fetch-based API client
- `ui/web/style.css` — dark+light themes

## API contract

Both UIs consume the same Phase 53 REST API:

```
POST /v1/chat/completions  →  send message, get response
GET  /v1/models            →  verify server is running
```

No new API endpoints needed — the LM Studio-compatible format already covers everything needed for a chat UI.

## Key details

- **Decoupled:** UI projects are fully separate from the C# codebase. They only depend on the REST API contract.
- **Godot-specific rendering:** The Godot UI can render game states natively (word game letters, hang-man scaffold, quiz score display) by parsing the response category from the API metadata.
- **Web UI is optional:** The Godot UI is the primary target; the web UI is a lightweight fallback for quick testing.
- **No Godot in CI:** The Godot project is optional and not part of the build/test pipeline.
- **Phase 55 is explicitly deferred:** Only build after Phase 53 (REST API) and Phase 54 (Engine/UI Separation) are complete.

## Verify

- `dotnet build` — succeeds (no C# changes)
- `dotnet test` — all pass
- Godot UI: `godot --path ui/godot` — launches and connects to running REST API
- Web UI: open `ui/web/index.html` — connects to REST API
