# Phase 46 — Timeline / Journal

Build a chronological summary of what the user said across sessions or within a session. "This week: Monday you started a new job. Wednesday you said you liked the team."

## Design

- Query `Facts` table ordered by `MentionedAt`, grouped by date
- Filter to the current session or a configurable window (last N days)
- Format as a bulleted timeline: "• {Day}: you {verb} {object}."
- Apply `ConjugateVerb` for third-person formatting of the stored verb
- Trigger: "what happened this week", "what did I say yesterday", "journal", "timeline", "recap my week"
- Proactive: 1-in-10 chance after session reaches 5+ turns, offers "Would you like me to recap what we've discussed?"

## Modified files

- `Knowledge/KnowledgeStore.cs` — add `GetFactsInDateRange(int userId, DateTime? from, DateTime? to)`, `BuildTimeline(List<Fact> facts)`
- `Responses/ResponseEngine.cs` — add `HandleTimelineRequest()` called in `GenerateResponse` before temporal query handling, and `BuildProactiveTimelineOffer()` in proactive fallback
- `Data/DbSeeder.cs` — seed `timeline_response` (3 templates), `timeline_empty` (2), `timeline_offer` (2) bot response categories
- `tests/PokeChat.Tests/Helpers/TestDataHelper.cs` — matching seed data

## Key details

- Date grouping: split facts by `MentionedAt` date (UTC), ordered chronologically
- Day labels: "Monday", "Tuesday", etc. (not full dates) for natural feel
- Same-session: "Earlier you mentioned..." vs cross-session: "Last session you said..."
- Empty state: "I don't have many memories from that time yet."
- Proactive offer only fires once per session (`TimelineOffered` context key)
- No new tables, no EF Core migration

## Tests (5 new)

1. `GetFactsInDateRange_ReturnsCorrectFacts`
2. `BuildTimeline_FormatsFacts_WithDayLabels`
3. `HandleTimelineRequest_ExplicitTrigger_ReturnsTimeline`
4. `HandleTimelineRequest_EmptyRange_ReturnsEmptyMessage`
5. `BuildProactiveTimelineOffer_ReturnsOffer`

## Verify

- `dotnet build` — succeeds
- `dotnet test` — all pass
