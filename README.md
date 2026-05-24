# PokeChat

A terminal chat bot with custom NLP — learns from conversations and stores knowledge in SQLite. No LLMs.

## AI Tools Disclosure

This project is an experiment with [opencode](https://opencode.ai) and various AI models to explore autonomous software engineering capabilities.

## Quick Start

```bash
dotnet build
dotnet run
```

Type `quit` or `exit` to leave.

## Tests

```bash
dotnet test
```

## Architecture

```
Program.cs                    → entry point, creates ChatSession
Core/
  ChatSession.cs              → main loop: greet → parse → respond → store
  GreetingPool.cs             → loads random greeting from DB
  ContextKeys.cs              → constants for context tracker
  PredicateType.cs            → enum for predicate classification
  INounCategoriser.cs         → interface for noun categorisation
  NounCategoriser.cs          → DB + heuristics (person/place/thing)
NLP/
  Tokeniser.cs                → British English tokenisation
  PosTagger.cs                → DB-loaded POS dictionary + heuristics
  SvoExtractor.cs             → Subject-Verb-Object triple extraction
  SentenceSplitter.cs         → multi-sentence splitting
  PunctuationHelper.cs        → shared IsPunctuation utility
  SpellChecker.cs             → Levenshtein spell correction
Math/
  IMathEngine.cs              → math evaluation interface
  SimpleMath.cs               → binary expression evaluator (+, -, *, /, ^)
Knowledge/
  KnowledgeStore.cs           → EF Core repository layer
  Fact.cs                     → Facet model for facts
  ContextTracker.cs           → conversation context, pronoun resolution
Responses/
  ResponseEngine.cs           → rule-based response generation
  ResponseRules.cs            → DB-loaded regex rules
Data/
  PokeChatDbContext.cs        → EF Core DbContext
  DbSeeder.cs                 → seeds initial data on first run
  Schema.sql                  → reference DDL for all tables
```

## Database

SQLite via EF Core. Location: `pokechat.db` in project root (auto-created).

All conversational data is persisted:
- **Users** — recognised by name, tracked across sessions
- **Facts** — SVO triples with predicate type (preference, possession, belief, etc.)
- **Conversations** — full turn-by-turn history
- **Greetings** — randomised welcome messages
- **Response rules** — regex patterns with response templates
- **POS dictionary** — ~2850 words (British + American English)
- **Bot responses** — template strings for all response categories
- **Misspellings** — common errors with corrections
- **Word definitions** — user-taught vocabulary
- **Word links** — synonyms, antonyms, related words
- **Noun categories** — person/place/thing classification

## Improvements (Phases)

| Phase | Description | Status |
|-------|-------------|--------|
| 1 | Critical bug fixes | ✅ |
| 2 | High-priority refactoring | ✅ |
| 3 | Medium-priority improvements | ✅ |
| 4 | Polish (low-priority) | ⬜ |
| 5 | British English adoption | ✅ |
| 6 | Simple mathematics | ✅ |
| 7 | Self-learning dictionary | ✅ |
| 8 | Noun categorisation | ✅ |
| 9 | Proactive conversation | ✅ |

See `.agents/plan.md` for full details. 
