# PokeChat — Agent Notes

## Project
- C# console app, .NET 10 (`net10.0`)
- Single project: `PokeChat.csproj` (solution: `PokeChat.slnx`)
- SQLite via **`Microsoft.EntityFrameworkCore.Sqlite`** (EF Core, not raw `Microsoft.Data.Sqlite`)
- **`Facet`** + **`Facet.Extensions.EFCore`** for entity-to-model mapping (`[Facet(typeof(FactEntity))]` partial class)
- Dependencies: `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Design`, `Facet`, `Facet.Extensions.EFCore`

## Commands
```bash
dotnet build          # build
dotnet run            # run the chat application
```

No test, lint, or typecheck scripts configured yet.

## Architecture
Terminal chat bot with custom NLP parser (no LLMs). Learns facts from conversations and stores them in SQLite via EF Core. All conversational data (greetings, response rules, POS dictionary, name patterns, bot commands) is stored in DB — the bot learns and grows its vocabulary over time.

```
Program.cs                    → entry point, creates ChatSession
Core/
  ChatSession.cs              → main loop: greet → parse → respond → store (implements IDisposable)
  GreetingPool.cs             → loads random greeting from DB via KnowledgeStore
NLP/
  Tokenizer.cs                → whitespace + punctuation tokenization
  PosTagger.cs                → DB-loaded dictionary (pos_dictionary table) + heuristics
  SvoExtractor.cs             → Subject-Verb-Object triple extraction
  SentenceSplitter.cs         → multi-sentence splitting on `.`, `!`, `?`
Knowledge/
  KnowledgeStore.cs           → EF Core repository layer over PokeChatDbContext
  Fact.cs                     → Facet model mapping to FactEntity
  ContextTracker.cs           → conversation context, pronoun resolution
Responses/
  ResponseEngine.cs           → rule-based response generation (checks facts, follow-ups)
  ResponseRules.cs            → loads rules from DB (response_rules table), regex matching
Data/
  PokeChatDbContext.cs        → EF Core DbContext with DbSets for all entities
  DbSeeder.cs                 → seeds initial data (greetings, rules, POS dictionary, etc.)
  Schema.sql                  → tables: users, facts, conversations, greetings, greeting_words, response_rules, response_rule_responses, pos_dictionary, name_patterns, bot_commands
  Entities/                   → entity classes: User, FactEntity, Conversation, Greeting, GreetingWord, ResponseRule, ResponseRuleResponse, PosDictionaryEntry, NamePattern, BotCommand
```

## Key Details
- **DB location:** `pokechat.db` in project root (resolved by walking up from `BaseDirectory` to find `PokeChat.csproj`)
- **DB init:** `Database.EnsureCreated()` in `PokeChatDbContext` constructor (no embedded resource or file walking needed for schema)
- **Seeder:** `DbSeeder.Seed()` populates greetings, greeting words, response rules, POS dictionary, name patterns, and bot commands on first run
- **Knowledge extraction:** "my name is Alice" → (user, is_named, Alice); "I like pizza" → (user, likes, pizza); "the sky is blue" → (sky, is, blue) [general knowledge]
- **Pronoun resolution:** ContextTracker resolves "it/this/that" → last object, "he/she/they" → last subject
- **Response flow:** pattern match from DB rules → check existing facts → random follow-up from known facts → default fallback
- **PosTagger:** Initialized from `pos_dictionary` table via `Initialize(List<PosDictionaryEntry>)` — no hardcoded dictionary in code
- **Response rules:** Loaded from `response_rules` + `response_rule_responses` tables (regex patterns with responses)
- **Greeting learning:** When user responds to name prompt with a novel first word, it's learned as a greeting word
- **Name extraction:** Uses `name_patterns` table (e.g. "my name is", "i am", "call me") to extract names from input
- **Bot commands:** Exit commands loaded from `bot_commands` table (`quit`, `exit`, etc.)
- **ChatSession:** Implements `IDisposable` to clean up the DbContext

## DB Schema
- `users` — id, name (unique), first_seen, last_seen
- `facts` — id, user_id (nullable FK→users), subject, verb, object, predicate_type, created_at
- `conversations` — id, user_id (nullable FK→users), user_input, bot_response, timestamp
- `greetings` — id, text, is_system, created_at
- `greeting_words` — id, word (unique), learned_from_user_id (nullable FK→users), created_at
- `response_rules` — id, pattern, input_type, is_active, created_at
- `response_rule_responses` — id, rule_id (FK→response_rules, CASCADE), response_text
- `pos_dictionary` — id, word, word_type, created_at
- `name_patterns` — id, pattern, created_at
- `bot_commands` — id, command (unique), created_at

## Git
- `.gitignore` excludes `/bin`, `/obj`, `/graphify-out`
- `pokechat.db` IS gitignored now
- `mempalace.yaml` and `entities.json` are gitignored
