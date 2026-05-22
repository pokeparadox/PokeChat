# PokeChat — Agent Notes

## Project
- C# console app, .NET 10 (`net10.0`)
- Single project: `PokeChat.csproj` (solution: `PokeChat.slnx`)
- SQLite via `Microsoft.Data.Sqlite` NuGet package

## Commands
```bash
dotnet build          # build
dotnet run            # run the chat application
```

No test, lint, or typecheck scripts configured yet.

## Architecture
Terminal chat bot with custom NLP parser (no LLMs). Learns facts from conversations and stores them in SQLite.

```
Program.cs           → entry point, creates ChatSession
Core/
  ChatSession.cs     → main loop: greet → parse → respond → store
  GreetingPool.cs    → random greeting selection
NLP/
  Tokenizer.cs       → whitespace + punctuation tokenization
  PosTagger.cs       → dictionary-based POS tagging + heuristics
  SvoExtractor.cs    → Subject-Verb-Object triple extraction
  SentenceSplitter.cs→ multi-sentence splitting
Knowledge/
  KnowledgeStore.cs  → SQLite-backed fact storage/retrieval
  Fact.cs            → fact data model
  ContextTracker.cs  → conversation context, pronoun resolution
Responses/
  ResponseEngine.cs  → rule-based response generation
  ResponseRules.cs   → pattern → response definitions
Data/
  DbConnection.cs    → SQLite connection + schema init
  Schema.sql         → tables: users, facts, conversations
```

## Key Details
- **DB location:** `pokechat.db` in project root (not embedded resource)
- **Schema init:** `DbConnection` tries embedded resource → `bin/Data/Schema.sql` → project root `Data/Schema.sql` (walks up from BaseDirectory)
- **Knowledge extraction:** "my name is Alice" → (user, is_named, Alice); "I like pizza" → (user, likes, pizza)
- **Pronoun resolution:** ContextTracker resolves "it/this/that" → last object, "he/she/they" → last subject
- **Response flow:** pattern match → check existing facts → random follow-up from known facts → default fallback

## DB Schema
- `users` — id, name, first_seen, last_seen
- `facts` — id, user_id (nullable), subject, verb, object, predicate_type, created_at
- `conversations` — id, user_id, user_input, bot_response, timestamp

## Git
- `.gitignore` excludes `/bin`, `/obj`, `/graphify-out`
- `pokechat.db` is NOT gitignored — consider adding if you don't want DB in repo
