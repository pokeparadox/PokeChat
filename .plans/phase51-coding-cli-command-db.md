# Phase 51 — Coding: CLI Command Database

Map natural language coding requests to shell commands via the persona's response rules. "Build the project" → `dotnet build`, "run the tests" → `dotnet test`, "commit my changes" → `git commit`.

## Design

- Seed ~200 common CLI commands as coding-persona response rules
- Each rule maps an NL pattern to a shell command via `{tool:shell:{command}}` marker
- Commands are grouped by category: build, test, git, package, run, lint, db, docker
- Variable substitution: `{project}` → current project name, `{file}` → CurrentFile, `{branch}` → CurrentBranch
- Multi-step commands: "commit and push" → run git add → git commit → git push sequentially
- Confirmation before destructive commands: "push to main? (yes/no)" via PendingConfirmation context key
- Output is captured and displayed; if output contains errors, route to error KB (Phase 52)

## Modified files

- `Data/DbSeeder.cs` — add `SeedCodingCommands()` with ~200 rules across command categories. Each rule has `Persona = "coding"`.
- `Knowledge/KnowledgeStore.cs` — add `GetPersonaResponseRules(string persona, string? category)` for filtered loading
- `Responses/ResponseRules.cs` — accept `persona` filter in `LoadRules()`; prefer persona-specific rules over null-persona rules
- `Core/ContextKeys.cs` — add `PendingConfirmation`, `PendingConfirmationCommand`, `PendingConfirmationArgs`
- `Core/ChatSession.cs` — add `TryHandleConfirmation(string input)` for destructive command confirmation; wire into `ProcessInput` before normal flow when `PendingConfirmation` is set
- `tests/PokeChat.Tests/Helpers/TestDataHelper.cs` — seed 20 representative coding commands for tests

## Command categories (200 total)

| Category | Count | Examples |
|----------|-------|---------|
| Build | 15 | `build`, `rebuild`, `build release`, `build project X` |
| Test | 15 | `test`, `test specific`, `test category`, `test file` |
| Git | 40 | `status`, `log`, `diff`, `commit`, `push`, `pull`, `branch`, `merge`, `stash` |
| Run | 10 | `run`, `run project`, `run with args` |
| Package | 15 | `add package`, `remove package`, `list packages`, `update package` |
| Lint/check | 10 | `lint`, `format`, `analyze`, `typecheck` |
| DB | 10 | `migration add`, `migration remove`, `update database` |
| File | 25 | `list files`, `find file`, `search in files`, `read file`, `edit file` |
| Dotnet generic | 20 | `clean`, `restore`, `publish`, `pack` |
| Docker | 15 | `build image`, `compose up`, `compose down`, `ps` |
| Misc | 25 | `kill port`, `check disk`, `show path`, `zip`, `unzip`, `curl` |

## Key details

- Commands use existing `{tool:shell:...}` MCP marker infrastructure from Phase 29b
- Variable substitution: `{file}` → `_context.GetContext(ContextKeys.CurrentFile)`, `{branch}` → `_context.GetContext(ContextKeys.CurrentBranch)`, etc.
- Destructive commands (push, deploy, delete, drop, rm -rf) require confirmation
- Confirmation: "Are you sure you want to `git push --force`? (yes/no)"
- Output is returned verbatim from MCP shell tool, capped at 2000 chars
- No new tables, no EF Core migration
- **LLM fallback for unknown commands:** if no rule matches the user's request, LLM offer fires → LLM generates the shell command → bot verifies syntax → stores as a new learned command rule. Future uses skip the LLM.
- **Self-healing commands:** if a command fails with an error, Phase 52's error KB diagnoses it. If the KB has no match, LLM fallback analyses the error and suggests a fix — then learns that fix.

## Tests (6 new)

1. `BuildCommand_ExecutesDotnetBuild`
2. `GitStatus_ExecutesGitCommand`
3. `DestructiveCommand_RequiresConfirmation`
4. `ConfirmationYes_ExecutesCommand`
5. `ConfirmationNo_Aborts`
6. `VariableSubstitution_ReplacesFileAndBranch`

## Verify

- `dotnet build` — succeeds
- `dotnet test` — all pass
