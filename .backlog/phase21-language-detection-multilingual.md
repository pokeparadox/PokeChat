# Phase 21 — Language Detection / Multi-Language Support

Detect the user's language and adapt the bot's dictionary, responses, and processing pipeline accordingly. Start with French and Spanish support alongside English.

## Design

### Language detection

Heuristic-based (no ML):
1. Check for language-specific stop words/function words: "le/la/les" (French), "el/la/los" (Spanish), "the/a/an" (English)
2. Score each candidate language based on matches in the input
3. Fall back to English if no clear winner

### Language registry

New `languages` table stores supported languages and their metadata.

| Column | Type | Notes |
|--------|------|-------|
| Id | int PK | auto-increment |
| Code | string | unique, e.g. "en", "fr", "es" |
| Name | string | "English", "French", "Spanish" |
| IsActive | bool | default true |

### Language-specific data

- Separate `pos_dictionary` entries per language (add `LanguageCode` column)
- Separate `bot_responses` per language (add `LanguageCode` column)
- Separate `response_rules` per language (add `LanguageCode` column)

### Per-user language

Add `PreferredLanguage` column to `users` table. Auto-detected on first interaction, can be overridden by user.

## Database changes

### New table: `languages`

As described above.

### Schema changes: add `LanguageCode` column to

- `pos_dictionary` (default "en")
- `bot_responses` (default "en")
- `response_rules` (default "en")
- `response_rule_responses` (default "en")
- `greetings` (default "en")
- `greeting_words` (default "en")
- `bot_commands` (default "en")

### Schema changes: users table

Add `PreferredLanguage` (string, default "en", FK → languages.Code).

## Modified files

- `Data/PokeChatDbContext.cs` — `DbSet<Language>`, fluent config for new columns
- `Data/Schema.sql` — DDL updates
- `Data/DbSeeder.cs` — `SeedLanguages()`, make existing seed data language-aware (duplicate for fr/es, at minimum greetings + common POS entries)
- `Data/pos_dictionary.json` — add language code to each entry
- `Core/ChatSession.cs` — run language detection on first input, store in user profile, filter all queries by user's language
- `NLP/` — `ITokeniser`, `IPosTagger`, `ISentenceSplitter` may need language-aware implementations or a factory pattern
- `Knowledge/KnowledgeStore.cs` — add `LanguageCode` parameter to all query methods: `GetGreetings(lang)`, `GetBotResponses(category, lang)`, `GetPosDictionary(lang)`, etc.
- `Responses/ResponseRules.cs` — language-filtered rule loading
- `Responses/ResponseEngine.cs` — pass language context through all template lookups

## Language detection logic

`DetectLanguage(string input)`:

1. Tokenise input
2. For each supported language, count how many language-specific stop words appear:
   - English: the, a, an, is, are, was, were, do, does, did, have, has, had, I, you, he, she, it, we, they
   - French: le, la, les, un, une, des, est, suis, sont, ai, as, a, avons, avez, ont, je, tu, il, elle, nous, vous, ils, elles
   - Spanish: el, la, los, las, un, una, es, soy, eres, somos, sois, son, he, has, ha, hemos, habeis, han, yo, tu, el, ella, nosotros, vosotros, ellos, ellas
3. Highest match score wins (minimum threshold of 2 matches, else default to English)
4. Store result in context

## Seed data approach

Phase 1: Start with English-only POS dictionary and add language codes. Then duplicate ~200 core French and ~200 core Spanish POS entries. Seed French/Spanish greetings (3–5 each). Seed French/Spanish bot responses for all categories (at minimum the 15 most common categories).

## Bot response translations

Priority categories for translation:
1. greeting
2. default_response
3. name_intro, name_confirm
4. farewell
5. empathy_* (from Phase 15)
6. bot_reset_warning, bot_reset_confirmed, bot_reset_cancelled

## New bot response categories

| Category | Purpose | Example |
|----------|---------|---------|
| `lang_auto_detected` | Announcing detected language | "I'll speak {0} with you!" (or equivalent) |
| `lang_switch_request` | User asks to switch language | "Can we speak French?" → "D'accord !" |
| `lang_not_supported` | Language not supported | "I don't know {0} yet. Can we use English?" |

## Flow

1. First user input → `DetectLanguage()` → store in user profile
2. User's language filters all subsequent DB queries
3. User can switch with "let's speak French" → `lang_switch_request` pattern
4. Greeting uses stored language preference on next session

## Tests

- `DetectLanguage_English_ReturnsEn`
- `DetectLanguage_French_ReturnsFr`
- `DetectLanguage_Spanish_ReturnsEs`
- `DetectLanguage_Unknown_FallsBackToEn`
- `KnowledgeStore.GetGreetings_FrenchFilter_ReturnsFrenchGreetings`
- `ChatSession.LanguageDetection_StoresInUserProfile`
- `ChatSession.LanguageSwitch_SwitchesLanguage`
- `dotnet test` — all pass

## Verify

```bash
dotnet build && dotnet test
```
