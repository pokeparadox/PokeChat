# Phase 45 — Preference Recommender

Make suggestions to the user based on their existing Preference/Dislike facts and `is_a` category WordLinks. "You like cats — people who like cats also often like dogs. Do you like dogs?"

## Design

- Query user's Preference facts: `(user, likes, X)`, `(user, loves, X)`, etc.
- For each liked object, walk `is_a` WordLinks upward to find categories: `cats → animals`, `pizza → food`
- Find *other* members of those categories connected via `is_a`
- Check if user already has a fact about that member — skip if known
- Suggest the unexplored member: "You like {liked}. People who like {liked} often also like {suggestion}. What do you think?"
- Trigger: proactive follow-up (1-in-8 chance after dead end, after proactive question and story/poetry slots)
- Limit: once per session (context key `RecommenderGiven`)

## Modified files

- `Knowledge/KnowledgeStore.cs` — add `GetUserPreferences(userId)`, `GetCategorySuggestions(List<string> likedItems)`, `IsFactKnown(userId, subject, verb, object)`
- `Core/ContextKeys.cs` — add `RecommenderGiven`
- `Responses/ResponseEngine.cs` — add `BuildRecommendation()` called in proactive fallback slot (after topic/proactive/story/poetry)
- `Data/DbSeeder.cs` — seed 4 `recommender` bot response templates
- `tests/PokeChat.Tests/Helpers/TestDataHelper.cs` — matching seed data

## Key details

- Skips if `RecommenderGiven` context key is set (one recommendation per session)
- Skips if user has < 2 Preference facts (not enough data)
- `GetCategorySuggestions` uses existing `GetCategoryChain` + `GetAllOfType` from Phase 18
- Dedup: skip items already in user's facts, skip items that ARE the liked item
- No new tables, no EF Core migration

## Tests (5 new)

1. `GetUserPreferences_ReturnsPreferenceFacts`
2. `GetCategorySuggestions_ReturnsRelatedItems`
3. `BuildRecommendation_ReturnsTemplatedSuggestion`
4. `BuildRecommendation_ReturnsNull_WhenFewerThanTwoPreferences`
5. `BuildRecommendation_SkipsAlreadyKnownFacts`

## Verify

- `dotnet build` — succeeds
- `dotnet test` — all pass
