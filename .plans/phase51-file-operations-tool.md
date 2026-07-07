# Phase 51 — Coding: File Operations Tool

Read, write, search, and list files as built-in tools, usable from the coding persona. Provides the core file operations that a coding assistant needs.

## Design

- New `ITool` implementations:
  - `ReadFileTool` — read a file (with optional offset/limit)
  - `WriteFileTool` — write content to a file (creates directories if needed)
  - `SearchTool` — grep/glob wrapper for content and file-name search
- All tools operate within the project root (path traversal blocked)
- Tool markers `{tool:read_file}`, `{tool:write_file}`, `{tool:search}` usable in response templates
- **LLM fallback:** When the user asks about file contents or code that doesn't match any tool rule, the LLM offer fires → LLM reads/reviews the file → stores the relevant info as a learned response pattern

## New files

- `Tools/ReadFileTool.cs` — `ITool`, reads file with optional offset/limit, validates path within project root
- `Tools/WriteFileTool.cs` — `ITool`, writes content, creates parent dirs, validates path
- `Tools/SearchTool.cs` — `ITool`, wraps grep (regex content search) and glob (filename pattern), returns matching files+lines

## Modified files

- `Tools/ToolRegistry.cs` — register new tools in `RegisterBuiltIn()` (only for coding persona)
- `Tools/tools.json` — add config sections for each tool (enabled, timeoutMs, maxResults)
- `Data/DbSeeder.cs` — seed coding persona response rules for file operations
- `tests/PokeChat.Tests/Helpers/TestDataHelper.cs` — matching seed data

## Config

```json
{
  "read_file": { "enabled": true, "timeoutMs": 5000, "maxFileSizeBytes": 1048576 },
  "write_file": { "enabled": true, "timeoutMs": 5000 },
  "search": { "enabled": true, "timeoutMs": 10000, "maxResults": 50 }
}
```

## Key details

- **Path traversal guard:** All file paths are resolved relative to project root, then checked that the resolved path starts with the project root directory. `../` escapes are rejected with an error message.
- `ReadFileTool` args: `["path", "offset=0", "limit=100"]` — offset and limit are optional, `limit=0` = no limit
- `WriteFileTool` args: `["path", "content"]` — creates file if not exists, overwrites if exists. Creates parent directories.
- `SearchTool` args: `["search_type", "pattern", "path=."]` — search_type is `"grep"` or `"glob"`, pattern is the search term, path is optional directory
- `maxFileSizeBytes` prevents reading very large files accidentally
- `maxResults` for search prevents overwhelming output
- **LLM fallback:** When the user says "show me the X file" or "what does the Y function do" and no tool rule matches, the LLM offer fires → LLM reads the file and returns a summary → bot stores the file path + summary as a learned pattern for future matching
- **Security:** Write operations only go to files within the project root; existing files get backed up to `.pokechat/backups/` before overwrite (optional, toggleable in config)

## Tests (9 new)

1. `ReadFileTool_ReadsExistingFile`
2. `ReadFileTool_PathTraversal_ReturnsError`
3. `ReadFileTool_FileTooLarge_ReturnsError`
4. `WriteFileTool_CreatesNewFile`
5. `WriteFileTool_OverwritesExistingFile`
6. `WriteFileTool_PathTraversal_ReturnsError`
7. `SearchTool_Grep_ReturnsMatches`
8. `SearchTool_Glob_ReturnsPaths`
9. `SearchTool_ExceedsMaxResults_Truncates`

## Verify

- `dotnet build` — succeeds
- `dotnet test` — all pass
