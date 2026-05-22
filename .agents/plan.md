# PokeChat — Implementation Plan

## Overview
A terminal-based chat application in C# (.NET 10) with SQLite and a custom NLP parser. No LLMs. All conversational data (greetings, response rules, POS dictionary) is stored in SQLite — the bot learns and grows its vocabulary over time.

## Architecture

```
PokeChat/
├── PokeChat.csproj
├── Program.cs               # Entry point, creates ChatSession
├── Core/
│   ├── ChatSession.cs       # Main loop: greet → parse → respond → store
│   └── GreetingPool.cs      # Loads greetings from DB
├── NLP/
│   ├── Tokenizer.cs         # Whitespace + punctuation tokenization
│   ├── PosTagger.cs         # DB-loaded dictionary + heuristics
│   ├── SvoExtractor.cs      # Subject-Verb-Object triple extraction
│   └── SentenceSplitter.cs  # Multi-sentence splitting
├── Knowledge/
│   ├── KnowledgeStore.cs    # Repository layer over DbContext
│   ├── Fact.cs              # Entity model
│   └── ContextTracker.cs    # Conversation context, pronoun resolution
├── Responses/
│   ├── ResponseEngine.cs    # Rule-based response generation
│   └── ResponseRules.cs     # Loads rules from DB, matches patterns
└── Data/
    ├── PokeChatDbContext.cs # EF Core DbContext
    ├── DbSeeder.cs          # Seeds initial data on first run
    └── Schema.sql           # DB schema (source of truth)
```

## SQLite Schema

### Core tables
- `users` — id, name, first_seen, last_seen
- `facts` — id, user_id (nullable), subject, verb, object, predicate_type, created_at
- `conversations` — id, user_id, user_input, bot_response, timestamp

### Data-driven tables (learnable)
- `greetings` — id, text, is_system, created_at
- `greeting_words` — id, word (unique), learned_from_user_id (nullable), created_at
- `response_rules` — id, pattern (regex), input_type, is_active, created_at
- `response_rule_responses` — id, rule_id (FK→response_rules), response_text
- `pos_dictionary` — id, word, word_type, created_at
  - word_type: pronoun, determiner, preposition, conjunction, verb, adjective, noun, stop_word
- `name_patterns` — id, pattern, created_at (e.g. "my name is", "i am", "call me")
- `bot_commands` — id, command, created_at (e.g. quit, exit, bye)

**Location:** `pokechat.db` (project root)

## EF Core — Database-First

- **Schema.sql** is the source of truth
- **EF Core Power Tools** reverse-engineers entities + DbContext from the database
- Entities: `User`, `Fact`, `Conversation`, `Greeting`, `GreetingWord`, `ResponseRule`, `ResponseRuleResponse`, `PosDictionary`, `NamePattern`, `BotCommand`
- `PokeChatDbContext` configured with SQLite provider
- `DbSeeder` populates initial data if tables are empty (migrates from hardcoded → data-driven)

## NLP Pipeline (from scratch)
1. **Tokenizer** — whitespace + punctuation separation
2. **POS Tagger** — DB-loaded dictionary (pos_dictionary table) + heuristics (capitalized = proper noun, -ing = verb, etc.)
3. **SVO Extractor** — finds verbs, walks left for subject, right for object
4. **Sentence Splitter** — splits on `.`, `!`, `?`

## Knowledge Extraction
- "my name is Alice" → (user, is_named, Alice)
- "I like pizza" → (user, likes, pizza)
- "the sky is blue" → (sky, is, blue) [general knowledge]
- "my dog is named Rex" → (user, has_pet_named, Rex)

## Response Engine
- Loads rules from DB (response_rules + response_rule_responses)
- Detects statement / question / greeting via regex pattern matching
- Check known facts → generate follow-ups
- Acknowledge new facts
- Reference past knowledge with context tracking

## Greeting Learning
When user responds to name prompt with a greeting word not in DB, it gets auto-added:
- "Hi, my name is Alice" → detects "hi" → adds to `greeting_words` if new
- Extracts name using `name_patterns` table

## Main Flow
1. Random greeting from DB → ask name
2. Identify/create user in DB
3. Loop: read input → split → parse → extract → store → respond
4. Track context for pronoun resolution
5. Learn new greeting words, store new facts

## Dependencies
- .NET 10
- Microsoft.EntityFrameworkCore.Sqlite (NuGet)
- Microsoft.EntityFrameworkCore.Design (NuGet, tooling)

## Steps
1. Replace `Microsoft.Data.Sqlite` with EF Core packages
2. Update `Schema.sql` with new data-driven tables
3. Create DB from schema, reverse-engineer with EF Core Power Tools
4. Create `PokeChatDbContext` and entity classes
5. Create `DbSeeder` with all current hardcoded data
6. Rewrite `KnowledgeStore` as repository layer over DbContext
7. Rewrite `GreetingPool` to load from DB
8. Rewrite `ResponseRules` to load from DB
9. Rewrite `PosTagger` to load dictionary from DB
10. Update `ChatSession` — greeting learning, DB-loaded patterns/commands
11. Update `ResponseEngine` to use DB-loaded rules
12. Replace `DbConnection` with DbContext initialization
13. Delete existing `pokechat.db`, rebuild and seed
14. Test end-to-end
