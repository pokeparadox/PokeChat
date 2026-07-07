# Plan 004: Implement Scheduling/Reminder Functionality

## Problem Description
- User explicitly asks "remind me to take out the rubbish" (Session 1 Turn 11)
- Bot immediately ignores and starts asking follow-up questions instead of creating/reminding
- Shows bot is missing core feature implementation capability
- Pattern suggests all user requests are being treated as conversation topics rather than action items

## Root Cause Analysis
1. No actual API integration for reminders/scheduling functionality
2. Bot treats every request as a knowledge-extraction task, not action-execution task
3. Missing `RequestHandler` that routes different input types to appropriate processors
4. Current architecture only supports Q&A mode, no command execution

## Implementation Plan

### Phase 1: Command Recognition System (Priority: High)
- [ ] Create `CommandParser` class to identify user requests as actions vs questions
- [ ] Implement keyword detection for scheduling commands ("remind me", "set reminder")
- [ ] Add priority system: action > question when both detected in same input

### Phase 2: Reminder Service Integration (Priority: High)
- [ ] Create `ReminderService` class to handle actual reminders/scheduling
- [ ] Integrate with existing notification/email API infrastructure
- [ ] Store reminder tasks with due dates and priority levels
- [ ] Implement cron-style scheduling for periodic notifications

### Phase 3: Request Routing Architecture (Priority: Medium)
- [ ] Create `RequestHandler` to route inputs to appropriate processors
- [ ] Distinguish between questions, commands, statements, and other input types
- [ ] Execute actions before generating follow-up conversation responses
- [ ] Log all executed actions for audit trail

## Acceptance Criteria
1. Bot creates actual reminders when user says "remind me to..."
2. Reminders persist across sessions (stored in database)
3. Reminder notifications can be sent via email/push system
4. User requests are processed before bot asks follow-up questions

## Estimated Effort: 8-10 hours
## Dependencies: Plan 001 (context storage needed for reminder persistence), notification service integration required
