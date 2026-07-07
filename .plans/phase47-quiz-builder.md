# Phase 47 — Quiz Builder

Flip stored facts into interactive multi-turn quizzes. The bot asks a question derived from something the user previously told it, and checks the answer.

## Design

- Pick a random fact from the user's stored facts
- Convert it to a question using templates based on PredicateType:
  - GeneralFact: "You told me {subject} {verb} {object}. What {verb} {subject}?"
  - Preference: "You said you {verb} {object}. Do you still {verb} {object}?"
  - PersonalAttribute: "You said you're {object}. Is that still true?"
- Accept short answers and match against the stored fact's object (case-insensitive substring match)
- Two modes: **explicit** ("quiz me") and **proactive** (1-in-12 chance after dead end)
- Track score: `QuizScore` context key (correct / total), display at end: "You got 3/5 correct!"
- Quit mid-quiz: "stop quiz", "give up" → show current score
- Max 5 questions per quiz session, then show final score

## Modified files

- `Knowledge/KnowledgeStore.cs` — add `GetRandomFactsForQuiz(int userId, int count)`
- `Core/ContextKeys.cs` — add `QuizActive`, `QuizScore`, `QuizQuestionCount`, `QuizCurrentAnswer`, `QuizFacts`
- `Core/ChatSession.cs` — add `TryHandleQuizStart`, `HandleQuizTurn`, `CheckQuizAnswer`, `ClearQuizState`, `UpdateQuizScore`, `GetQuizResponse`. Wire into `ProcessInput` after joke/riddle routing, before game routing.
- `Data/DbSeeder.cs` — seed `quiz_start` (3), `quiz_question` (4 per PredicateType template), `quiz_correct` (2), `quiz_wrong` (2), `quiz_score` (2), `quiz_give_up` (2), `quiz_already_active` (2), `quiz_no_facts` (2) bot response categories
- `tests/PokeChat.Tests/Helpers/TestDataHelper.cs` — matching seed data

## Key details

- Quiz questions are stored in a context key as JSON: `[{"factId": 1, "question": "...", "answer": "..."}, ...]`
- Answer matching: case-insensitive `Contains` on the object string (e.g. "Paris" matches "the capital is Paris")
- Wrong answer shows the correct one: "Not quite! The answer was {answer}."
- Empty state: user has < 3 facts → "I don't know enough about you to make a quiz yet."
- Quiz survival: answers reset if user gives non-answer input (quiz stays active but same question repeats)
- No new tables, no EF Core migration

## Tests (7 new)

1. `TryHandleQuizStart_TriggersOnPhrase`
2. `TryHandleQuizStart_TooFewFacts_ReturnsEmpty`
3. `HandleQuizTurn_CorrectAnswer_UpdatesScore`
4. `HandleQuizTurn_WrongAnswer_ShowsCorrect`
5. `HandleQuizTurn_GiveUp_RevealsScore`
6. `HandleQuizTurn_AfterMaxQuestions_ShowsFinalScore`
7. `TryHandleQuizStart_AlreadyActive_ReturnsPrompt`

## Verify

- `dotnet build` — succeeds
- `dotnet test` — all pass
