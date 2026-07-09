# PokeChat

A terminal chat bot with custom NLP — learns from conversations and stores knowledge in SQLite. No LLMs.
The eventual goal is to make a useful bot that prioritises efficient methods and then falls back to more complicated mthods only when needed.
I feel LLMs do this the wrong way around around.

## AI Tools Disclosure

This project is an experiment with [opencode](https://opencode.ai) and various AI models to explore autonomous software engineering capabilities.
I want to use LLMs in order to not use LLMs.

## Quick Start

```bash
dotnet build
dotnet run
```

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

### Self-Knowledge & Stats
| If you say… | The bot will… |
|---|---|
| `what do you know about me` / `tell me about myself` | List everything it knows about you |
| `how many facts do you know` / `what are my stats` | Show conversation statistics |
| `what did we talk about` / `summarise our conversation` | Summarise the session |
| `compliment me` / `say something nice` | Give a compliment based on your facts |

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
  Pluraliser.cs               → singularise English plural nouns
  ContractionExpander.cs      → expands contractions before tokenisation
Math/
  IMathEngine.cs              → math evaluation interface
  SimpleMath.cs               → binary expression evaluator (+, -, *, /, ^)
Knowledge/
  KnowledgeStore.cs           → EF Core repository layer
  Fact.cs                     → Facet model for facts
  ContextTracker.cs           → conversation context, pronoun resolution
Stories/
  StoryGenerator.cs           → random short story composition
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
| — | Interview Mode (LLM-driven / non-LLM, bot-asks questions) | ✅ |
| 40 | Word List Consolidation (shared GenerationUtils) | ✅ |
| 41 | Universal LLM Thinking Indicator | ✅ |
| 42 | Non-LLM Interview Mode (30-question bank) | ✅ |
| 43 | Hang-Man Game (POS-dictionary word guessing) | ✅ |
| 48 | Entity Graph Explorer (BFS path finding, relation queries) | ✅ |
| 49 | Persona System (coding/chat, persona-filtered rules) | ✅ |
| 50 | Shell Command Tool (whitelist-based security) | ✅ |
| 51 | File Operations Tool (read/write/list/search, path traversal protection) | ✅ |
| 50b | Coding Project Context (file mention detection, git branch) | ✅ |
| 51b | Coding CLI Command DB (~90 NL→shell mappings, destructive confirmation) | ✅ |
| 52 | Error Knowledge Base (~60 compiler errors, regex matching) | ✅ |
| 44 | Neural Net Integration (~250 seeds, IntentClassifier, self-training) | ✅ |
| 45 | Preference Recommender (suggestions from `is_a` WordLinks, once/session) | ✅ |
| 46 | Timeline / Journal (chronological fact recap by day, proactive offer) | ✅ |
| 47 | Quiz Builder (multi-turn quiz from user facts, correct/wrong/score) | ✅ |
| 53 | In-Chat Reminders (create/list/done/cancel, session-start due check) | ✅ |
| — | Name Confusion Fix (POS validation, cross-session identity) | ✅ |
| — | Meta-commentary Detection (confusion/not-helpful/mocking, repeated complaint tracking) | ✅ |
| — | Word Classification Expansion (all 6 word types, "I don't know" + LLM fallback) | ✅ |
| — | Sub-plan 1: Minimal HTTP API (`POST /chat`, `GET /health`, in-memory sessions) | ✅ |

See `.agents/history.md` for completed improvements. 
