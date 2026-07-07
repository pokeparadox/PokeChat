# Phase 53: In-Chat Reminders System

## Goal
Add a reminders system that lets users create, list, mark-done, and cancel in-chat reminders. No persona restriction — reminders work in all modes (chat, coding, pa).

## Entity & Schema

### `Reminder` table
- `Id` (INTEGER PK)
- `UserId` (INTEGER FK → users, NOT NULL)
- `Task` (TEXT, NOT NULL)
- `DueAt` (TEXT, ISO 8601 datetime)
- `Status` (TEXT, NOT NULL — "pending" / "done" / "cancelled", default "pending")
- `CreatedAt` (TEXT, ISO 8601)

## Time Parsing
- Reuse existing `TemporalExpression` table (Phase 17) for relative dates ("tomorrow" → +1 day, "next week" → +7 days, "in 3 days" → +3 days)
- Add regex for time-of-day: `at (\d{1,2})(?::(\d{2}))?\s*(am|pm)?`
- Default when no time given: **in 1 hour** from now
- Combine date offset + time into full `DueAt` DateTime

## Session-Start Behavior
- Query `WHERE Status == "pending" AND DueAt <= DateTime.UtcNow`
- Show **all** matching reminders (no cap) via `reminder_due` response category
- Overdue reminders (due before this session) are included
- Suppress if already shown 3 reminders in the current session (anti-spam)

## ProcessInput Integration
Runs after identity/reset/rename/correction but before games/quizzes in `ChatSession.ProcessInput()`:

### Triggers
| Input pattern | Action |
|---|---|
| `remind me {time} to {task}` | Single-pass: parse time + task, store, confirm |
| `remind me to {task}` (no time) | Create with **1 hour default** |
| `what reminders` / `what's coming up` / `what do I need to do` | List pending sorted by due_at |
| `mark {task} as done` / `I did {task}` / `I finished {task}` | Partial match → set status to "done" |
| `cancel reminder for {task}` / `forget about {task}` / `delete reminder` | Partial match → set status to "cancelled" |

- Multi-step flow: for ambiguous input (e.g., "remind me to call mum" without a time), bot prompts via context keys (`PendingReminderTask`, `PendingReminderTime`). On next turn, checks those keys first.
- Duplicate prevention: `CreateReminder` checks existing pending reminder with same `UserId` + same `Task` text; if found, returns warning and does not create duplicate.

## Duplicate Handling
- **Prevent duplicates**: `HasReminderForTask(userId, task)` checks existing pending reminder with matching task text (case-insensitive, trimmed)
- If found, bot responds with `reminder_duplicate` category: "You already have a reminder for that."

## Files to Modify/Create

| File | Change |
|---|---|
| `Data/Entities/Reminder.cs` | **New** — entity with Id, UserId, Task, DueAt, Status, CreatedAt |
| `Data/PokeChatDbContext.cs` | Add `DbSet<Reminder> Reminders`, fluent config (HasOne→User, no cascade delete) |
| `Data/Schema.sql` | Add `CREATE TABLE reminders (...)` |
| `Data/DbSeeder.cs` | Seed 7 bot response categories |
| `Core/ContextKeys.cs` | Add `PendingReminderTask`, `PendingReminderTime` constants |
| `Knowledge/KnowledgeStore.cs` | Add `CreateReminder()`, `GetPendingReminders()`, `GetUpcomingReminders()`, `MarkReminderDone()`, `CancelReminder()`, `ParseReminderTime()`, `HasReminderForTask()` |
| `Core/ChatSession.cs` | Add `TryHandleReminderRequest()`, `HandleReminderCreation()`, `HandleReminderAnswer()`, `HandleReminderList()`, `HandleReminderDoneCancel()`, session-start due-check hook |
| `Core/ResponseEngine.cs` | Load `reminder_*` response categories, expose `GetReminderResponses(category)` |

## Bot Response Categories (7)
- `reminder_created` — "I'll remind you to {task} at {time}."
- `reminder_due` — "By the way, you wanted me to remind you: {task}"
- `reminder_list` — "Here are your reminders:\n1. {task} at {time}\n..."
- `reminder_empty` — "You don't have any reminders set."
- `reminder_done` — "Marked '{task}' as done!"
- `reminder_cancelled` — "Cancelled the reminder for '{task}'."
- `reminder_duplicate` — "You already have a reminder for that."

## Tests (~12)
1. Create reminder with explicit time ("remind me at 5pm to water plants")
2. Create with relative date ("remind me tomorrow to do laundry")
3. Create with no time (defaults to 1 hour)
4. Duplicate prevention (same task text, same user)
5. List reminders (multiple pending)
6. List reminders (empty — falls to `reminder_empty`)
7. Mark done (exact task match)
8. Mark done (partial task match)
9. Cancel reminder
10. Session-start due check (with overdues)
11. Rich input ("remind me next tuesday at 3pm to call the dentist")
12. Normal chat unaffected when input does not mention reminders
