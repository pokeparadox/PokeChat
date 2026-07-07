# Grammar & Natural Flow Bot Testing

## Purpose
Run the PokeChat bot through realistic conversations and analyse responses for grammar bugs, unnatural phrasing, and conversational flow issues.

## How to use
1. Build the project: `dotnet build`
2. Delete existing DB: `rm -f pokechat.db pokechat.db-shm pokechat.db-wal` (in project root)
3. Create a test conversation script with realistic inputs covering:
   - Greeting → name introduction
   - Like/dislike statements (base form AND conjugated: "love", "loves")
   - Facts about other entities ("my sister likes X")
   - Sentiment expressions ("I'm sad", "I'm happy")
   - Multi-word objects ("Python is a programming language")
   - Short affirmative/negative responses ("yes", "no")
   - Math expressions
   - Dictionary queries
   - Temporal queries
   - Contradictory statements (like then hate the same thing)
   - Bot rename attempts
4. Pipe the script: `dotnet run --project . < /tmp/opencode/test.txt`
5. Analyse each bot response for:
   - Grammar errors (wrong verb conjugation, wrong pronoun)
   - Unnatural phrasing (e.g., "You seemed neutral earlier")
   - Wrong subject reference (e.g., "You like X" when user said sister likes X)
   - Context-inappropriate responses (e.g., asking about pizza after user says "I have a dog")
   - Missing empathy (e.g., no empathy when user says "I'm sad")
   - Fact corruption (e.g., "python is a" instead of "python is a programming language")

## Checklist of known bug patterns
- [ ] Greeting word accepted as name (single-token fallback in ExtractName)
- [ ] Conjugated verb not recognised ("loves" ≠ "love", "hates" ≠ "hate", etc.)
- [ ] "Neutral" used in emotion followup templates
- [ ] Empathy skipped on first emotional expression
- [ ] Proactive templates assume "you" subject for all facts
- [ ] Inference generalisation persists across turns
- [ ] SVO extractor splits on gerund/participle verbs
- [ ] Session summary shows un-conjugated verbs ("Kevin like pizza")
- [ ] "Do you still feel that way?" for factual statements
- [ ] Temporal confirmation uses future tense for past events

## Bug filing format
For each bug found, create or update the plan at `.plans/phase23-grammar-natural-flow-bugs.md` with:
- Bug ID (B1, B2, etc.)
- Severity (Critical, High, Medium, Low)
- File path and line number
- Root cause analysis
- Conversation evidence (exact input/output)
- Fix suggestion

## Running verification
```bash
dotnet build && dotnet test
```
