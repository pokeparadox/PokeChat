# Spell-Checking Feature Plan

## Overview
Add spell-checking to PokeChat: a `misspellings` DB table maps common typos → corrections, auto-corrects silently, and interactively learns new words from the user.

---

## Steps

### Step 1 — New Entity: `Misspelling`
`Data/Entities/Misspelling.cs`
- `Id` (int, PK)
- `Misspelling` (string, required, unique) — the wrong spelling
- `Correction` (string, required) — the correct word
- `CreatedAt` (string, required)

### Step 2 — DbContext changes (`Data/PokeChatDbContext.cs`)
- Add `DbSet<Misspelling> Misspellings`
- Fluent API: unique index on `Misspelling`, required fields, same pattern as `GreetingWord`

### Step 3 — Schema.sql
Add `CREATE TABLE IF NOT EXISTS misspellings (...)`

### Step 4 — KnowledgeStore methods (`Knowledge/KnowledgeStore.cs`)
- `GetMisspellings()` → `List<Misspelling>`
- `AddMisspelling(string misspelling, string correction)`
- `GetCorrection(string misspelling)` → `string?`
- `IsWordKnown(string word)` — checks existence in `pos_dictionary` table
- `AddLearnedWord(string word)` — adds to `pos_dictionary` with `word_type = "unknown"`

### Step 5 — New class: `NLP/SpellChecker.cs`
- `Initialize(HashSet<string> dictionary, Dictionary<string, string> misspellings)` — load from KnowledgeStore
- `AutoCorrect(List<string> tokens)` → silently applies known misspellings, returns corrected tokens
- `GetUnknownWords(List<string> tokens)` → returns tokens not in dictionary
- `SuggestCorrections(string word, int maxDistance = 2)` → Levenshtein distance against dictionary, returns ranked matches
- `HasSuggestions(string word)` → bool

### Step 6 — ChatSession changes (`Core/ChatSession.cs`)
- Add `SpellChecker _spellChecker` field
- Initialize in constructor (POS dictionary + misspellings from KnowledgeStore)
- `ProcessSentence()`: run `_spellChecker.AutoCorrect(tokens)` before `PosTagger.Tag()`
- Track unknown words per input in context
- Cross-turn interactive learning:
  - If `pending_clarification` is set in context, handle as clarification response:
    - User affirms a suggestion → `AddMisspelling()` + `AddLearnedWord()`
    - User explains unknown word → extract correction, add both to DB
  - Otherwise, run normal sentence processing

### Step 7 — ResponseEngine changes (`Responses/ResponseEngine.cs`)
- Accept `SpellChecker` and `ContextTracker` for unknown word awareness
- If unknown words exist and no higher-priority rule matched:
  - Suggestions available → "Did you mean 'X' instead of 'Y'?"
  - No suggestions → "I don't know the word 'Y'. What does it mean?"
- Store `pending_clarification` in ContextTracker when asking

### Step 8 — DbSeeder seed data (`Data/DbSeeder.cs`)
Seed common misspellings:
- `teh` → `the`, `recieve` → `receive`, `beleive` → `believe`, `wierd` → `weird`
- `adress` → `address`, `calender` → `calendar`, `definately` → `definitely`
- `occured` → `occurred`, `seperate` → `separate`, `tommorow` → `tomorrow`
- `alot` → `a lot`, `untill` → `until`, `wich` → `which`

### Step 9 — Verify
```bash
dotnet build
```
