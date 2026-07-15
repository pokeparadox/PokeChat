# PokeChat

## Overview

A terminal chat bot with custom NLP — learns from conversations and stores knowledge in SQLite. No LLMs.
The eventual goal is to make a useful bot that prioritises efficient methods and then falls back to more complicated methods only when needed.
I feel LLMs do this the wrong way around. They start with the trained model and then try to learn from the user, which is backwards. I want to start with the user and then fall back to LLMs only when needed.

## PokeChat.Api

Now the heart of the project. It runs as a REST API and handles all NLP, knowledge storage, and response generation. 
It exposes an OpenAI-compatible endpoint for chat completions, so you can use it with any OpenAI-compatible client (e.g. [ChatGPT](https://chat.openai.com), [LangChain](https://www.langchain.com/), etc.) or your own custom client.
The eventual goal is to have something that you could use as a coding assistant, which has memory and tools built in, but can also relay to a configured LLM as a fallback and learn from responses and interactions. 

## PokeChat (Console Application)

This is now a dumb, simple client application. All of the core code has been moved into the PokeChat.Api

## AI Tools Disclosure

This project is an experiment with [opencode](https://opencode.ai) and various AI models to explore autonomous software engineering capabilities.
I want to use LLMs in order to not have to use LLMs. 

## Quick Start

```bash
dotnet build
dotnet run --project Api/    # start the REST API
dotnet run                   # start the console client (connects to API)
```

The console client connects to `http://localhost:5000` by default.
Override with: `POKECHAT_API_URL=http://host:port dotnet run`

Type `quit` or `exit` to leave.

## Feature Reference

### Starting & Stopping
| If you say… | The bot will… |
|---|---|
| `hi` / `hello` / `hey` | Greet you and ask your name |
| `my name is X` / `I'm X` / `call me X` | Remember your name (validates against dictionary, verifies returning users) |
| `quit` / `exit` | Show a session summary and goodbye |
| `start fresh` / `reset everything` / `forget everything` | Warn then wipe your data |

### Learning New Words
| If you say… | The bot will… |
|---|---|
| *(any word it doesn't know)* | Ask what it means, or suggest a correction |
| `typo` / `never mind` / `forget it` | Cancel learning and move on |
| *(after teaching a word)* `person` / `place` / `thing` / `verb` / `adjective` / `noun` | Classify the word |
| `what is the definition of X` / `define X` | Look up or ask you for a definition |
| `what is another word for X` / `synonym for X` | Show related words |

### Teaching the Bot
| If you say… | The bot will… |
|---|---|
| `you should say X` / `say X instead` / `try saying X` | Learn a new response pattern |
| `that's not right` / `not what I meant` | Reduce confidence in its last response |
| `that's better` / `now you've got it` | Reinforce the last response |

### Maths
| If you say… | The bot will… |
|---|---|
| `2 + 2` / `10 * 5` / `3 ^ 4` | Calculate the result |
| `2 + 2 = 5` (wrong answer) | Correct you |
| `2 + 2 = 4` (right answer) | Confirm |

### Games & Fun
| If you say… | The bot will… |
|---|---|
| `tell me a story` / `make up a story` | Generate a short story |
| `write a haiku` / `write a limerick` | Compose a poem |
| `let's play a word game` / `story chain` | Start a collaborative story chain |
| `let's play mad libs` | Start a Mad Libs game |
| `tell me a joke` / `make me laugh` | Tell a dad joke (setup → punchline) |
| `tell me a riddle` / `riddle me` | Pose a riddle (up to 3 attempts + hints) |
| `would you rather` / `wyr` | Ask a Would You Rather from your facts |
| `let's play hangman` | Start a Hangman game |
| `magic 8 ball` / `will it` / `should i` | Fortune-telling prediction |

### Quiz
| If you say… | The bot will… |
|---|---|
| `quiz me` / `test me` / `give me a quiz` | Quiz you on facts it's learned |

### Reminders
| If you say… | The bot will… |
|---|---|
| `remind me to X at Y` / `set a reminder` | Create an in-chat reminder |
| `show reminders` / `list reminders` | Show pending reminders |
| `mark X as done` / `reminder done` | Complete a reminder |
| `cancel reminder X` / `remove reminder` | Cancel a reminder |

### Temporal & Timeline
| If you say… | The bot will… |
|---|---|
| `what did I do yesterday` / `what happened today` | Recall facts with that time context |
| `recap my day` / `what happened this week` / `timeline` | Show a timeline |

### Inference & Entity Queries
| If you say… | The bot will… |
|---|---|
| `does X verb Y` (e.g. "does Alice like cats") | Check if that relation exists |
| `how is X connected to Y` | Find a path between entities |
| `tell me about X` / `what is X` | List known connections for that entity |

### Bot Configuration
| If you say… | The bot will… |
|---|---|
| `can I call you X` / `I'll call you X` / `rename yourself X` | Rename the bot (per-user) |
| `switch to coding mode` / `enter coding mode` | Switch to coding persona (build, test, git, etc.) |
| `switch to chat mode` / `enter chat mode` | Switch back to chat persona |
| `interview mode` / `train the bot` | Start interactive bot training |

### Time & Timezone
| If you say… | The bot will… |
|---|---|
| `what's the time` / `what time is it` / `tell me the time` / `current time` | Tell the current time (UTC by default) |
| `what's the date` / `today's date` / `current date` | Show today's date |
| `what day is it` / `what day of the week` | Show the day of the week |
| *(add)* `in EST` / `in PST timezone` | Convert to that timezone |
| `my timezone is GMT` / `I'm in EST` / `set my time zone to PST` | Remember your timezone for future queries |

### Self-Knowledge & Stats
| If you say… | The bot will… |
|---|---|
| `what do you know about me` / `tell me about myself` | List everything it knows about you |
| `how many facts do you know` / `what are my stats` | Show conversation statistics |
| `what did we talk about` / `summarise our conversation` | Summarise the session |
| `compliment me` / `say something nice` | Give a compliment based on your facts |

### Feedback & Ratings
| If you say… | The bot will… |
|---|---|
| `~rate +1` / `~rate -1` / `~rate up` / `~rate down` | Rate the last response (+1 or -1) |
| `thanks` / `that was helpful` / `great answer` | Auto-rate +1 for the last response |
| `that didn't help` / `not helpful` / `bad answer` | Auto-rate -1 for the last response |

### Error Knowledge
| If you say… | The bot will… |
|---|---|
| *(paste a compiler error like `CS1009`, `CS0161`, `NullReferenceException`)* | Identify the error and suggest a fix |
| *(paste an error it doesn't recognise)* | Ask you to explain the fix |
| `the fix for that was X` (after an error suggestion) | Learn the new error pattern |

### Advanced Features
| If you say… | The bot will… |
|---|---|
| `search X` / `look up X` / `find X` | Perform a web search |
| `run command X` / `shell command X` | Execute a shell command (whitelisted) |
| *(in coding mode)* file ops, git, build, test commands | CLI command DB with destructive-command confirmation |
| *(mention a filename like `Program.cs`)* | Detect and track current file context |
| *(switch persona)* | Detect git branch automatically |

### Emotion & Sentiment
| If you express strong emotion… | The bot will… |
|---|---|
| *(sad / angry / happy / afraid / surprised phrasing)* | Respond with empathy (sentiment-aware) |
| *(you're an idiot / shut up)* | Respond with a calm deflection |
| `that doesn't make sense` / `i'm confused` / `not helpful` | Acknowledge and apologise; tracks repeated complaints |

## Tests

```bash
dotnet test
```

## Architecture

Two-project solution:
- **`Api/`** — Core library + REST API (all NLP, knowledge, response logic)
- **`PokeChat.csproj`** — Thin console HTTP client (~70 lines)

```
PokeChat.csproj              → console HTTP client (calls Api/)
Api/
  Program.cs                 → REST API endpoints (Minimal API)
  Core/
    ChatEngine.cs            → main loop: greet → parse → respond → store
    ChatSession.cs           → Console I/O wrapper delegating to ChatEngine
    GreetingPool.cs          → loads random greeting from DB
    ContextKeys.cs           → constants for context tracker
    PredicateType.cs         → enum for predicate classification
    INounCategoriser.cs      → interface for noun categorisation
    NounCategoriser.cs       → DB + heuristics (person/place/thing)
  NLP/
    Tokeniser.cs             → British English tokenisation
    PosTagger.cs             → DB-loaded POS dictionary + heuristics
    SvoExtractor.cs          → Subject-Verb-Object triple extraction
    SentenceSplitter.cs      → multi-sentence splitting
    PunctuationHelper.cs     → shared IsPunctuation utility
    SpellChecker.cs          → Levenshtein spell correction
    Pluraliser.cs            → singularise English plural nouns
    ContractionExpander.cs   → expands contractions before tokenisation
  Math/
    IMathEngine.cs           → math evaluation interface
    SimpleMath.cs            → binary expression evaluator (+, -, *, /, ^)
  Knowledge/
    KnowledgeStore.cs        → EF Core repository layer
    Fact.cs                  → Facet model for facts
    ContextTracker.cs        → conversation context, pronoun resolution
  Stories/
    StoryGenerator.cs        → random short story composition
  Responses/
    ResponseEngine.cs        → rule-based response generation
    ResponseRules.cs         → DB-loaded regex rules
  Data/
    PokeChatDbContext.cs     → EF Core DbContext
    DbSeeder.cs              → seeds initial data on first run
    Schema.sql               → reference DDL for all tables
```

## REST API

| Endpoint | Method | Description |
|---|---|---|
| `/health` | GET | Health check |
| `/v1/models` | GET | List available models (`pokechat-v1`, `pokecode-v1`) |
| `/v1/chat/completions` | POST | OpenAI-compatible chat (stream + non-stream) |
| `/v1/title` | POST | Generate a conversation title from message history |
| `/sessions` | POST/GET | Create or list sessions |
| `/sessions/{id}` | GET/DELETE | Get or end a session |
| `/sessions/{id}/chat` | POST | Send a message to a session |

### `/v1/title`

```json
POST /v1/title
{ "messages": [{ "role": "user", "content": "I'm getting a NullReferenceException in ChatEngine" }] }

→ { "title": "ChatEngine Debugging" }
```

Classifies the last user message into one of 9 categories (debugging, planning, feature, setup, testing, code_review, brainstorm, question, chat) and extracts a key subject to form a readable title. No LLM required — pure keyword + regex matching, returns in <1ms.

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
- **Emotion keywords** — ~95 keywords across 5 sentiments for sentiment analysis
- **Contractions** — 54 common English contractions mapped to expanded forms
- **User bot names** — per-user custom bot name assignment
- **Story templates** — 10+ slot-based templates for short story generation
- **Learned response rules** — user-taught response patterns with confidence tracking
- **Error knowledge base** — ~60 seeded compiler/runtime errors with regex patterns and fixes
- **Temporal expressions** — 15 seeded time expressions (yesterday, last week, etc.)
- **Inference word links** — `is_a` category chains for reasoning
- **Jokes & Riddles** — 20 dad jokes, 13 riddles with hints and difficulty levels
- **Rhyme groups** — 30+ entries for poetry generation
- **Poem templates** — haiku + limerick templates
- **Mad Libs templates** — slot-based Mad Libs stories
- **Intent model** — ~250 seeded training examples for intent classification
- **Conversation metrics** — per-session quality and response effectiveness tracking
- **Joke/Riddle/MadLibs/Quiz entities** — game state persistence

## Improvements (Phases)

| Phase | Description | Status |
|-------|-------------|--------|
| 1 | Critical bug fixes | ✅ |
| 2 | High-priority refactoring | ✅ |
| 3 | Medium-priority improvements | ✅ |
| 4 | Polish (low-priority) | ✅ |
| 5 | British English adoption | ✅ |
| 6 | Simple mathematics | ✅ |
| 7 | Self-learning dictionary | ✅ |
| 8 | Noun categorisation | ✅ |
| 9 | Proactive conversation | ✅ |
| 10 | Phrasing improvement (ConjugateVerb, template rewrite) | ✅ |
| 11 | Plural handling (Pluraliser, auto-learn plurals) | ✅ |
| 12 | Bot renaming (per-user custom bot name) | ✅ |
| 13 | EF Core migrations (schema-safe upgrades) | ✅ |
| 14 | Reset / start fresh (wipe user data, keep seeds) | ✅ |
| 15 | Emotion / sentiment awareness (empathy responses) | ✅ |
| 16 | Contractions handling (I'm, don't, etc. expanded before tokenisation) | ✅ |
|   | Temporal knowledge (yesterday, last week query support) | ✅ |
| 17 | Inference / simple reasoning (contradiction, generalisation, category chains) | ✅ |
| 18 | Session summarisation (per-session summaries, end-of-session recap) | ✅ |
| 19 | Self-learning response patterns (user corrections, confidence system) | ✅ |
| 20 | Multi-turn topic tracking (topic stack, cross-turn references) | ✅ |
| 22 | Conversation quality metrics (per-session stats, response effectiveness) | ✅ |
| 23 | Grammar & natural flow bugs (11 bugs fixed) | ✅ |
| 24 | Random short story generation (11-slot templates) | ✅ |
| 25 | Word classification (person/place/thing/verb follow-up) | ✅ |
| 26 | Chat log & session improvements (24 bug fixes) | ✅ |
| 27 | Built-in Tool Layer (WebSearch, ReadUrl) | ✅ |
| 28 | Full MCP Protocol (JSON-RPC stdio subprocess) | ✅ |
| 29 | Optional LLM support (Ollama-backed) | ✅ |
| 29b | Data-Driven MCP Tool Triggers (zero-code tool rules) | ✅ |
| 30 | Enhanced LLM Integration (AlwaysOn, summarisation, inference) | ✅ |
| 31 | Clarification/Classification Cancel | ✅ |
| 32 | End-of-Session LLM Homework Check | ✅ |
| 33 | Word Game UX Improvements (thinking indicator, grammar filter) | ✅ |
| 34 | Dad Jokes + Riddles (20 jokes, 8 riddles, multi-turn) | ✅ |
| 35 | Poetry Generation (haiku + limerick, syllable counting) | ✅ |
| 36 | Mad Libs + Would You Rather + Magic 8 Ball | ✅ |
| 37 | Cross-Session Recall (~30% chance at session start) | ✅ |
| 38 | Emoji Personality (117 category-appropriate emoji) | ✅ |
See `.agents/history.md` for completed improvements. 
