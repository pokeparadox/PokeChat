# Plan 002: Fix 💭 Emoji Follow-Up Pattern

## Problem Description
- Bot uses 💭 emoji to trigger nonsensical follow-up questions that don't make sense grammatically or logically
- Example from Session 1 Turn 4: User says "It's not something you really had to remember" → bot asks "What else can you share about that the energy that they and is converted with little waste as heat?"
- The question contains jumbled words ("that", "the energy") instead of coherent follow-up
- Pattern appears in every turn where 💭 emoji is used

## Root Cause Analysis
1. 💭 trigger likely fires after any user input regardless of context relevance
2. Follow-up generator doesn't validate grammatical correctness or semantic coherence
3. Bot may be extracting random phrases from previous knowledge instead of understanding actual conversation flow
4. No fallback mechanism when generated question would confuse the bot

## Implementation Plan

### Phase 1: Remove Automatic 💭 Generation (Priority: High)
- [ ] Disable automatic 💭 emoji based on any input pattern
- [ ] Keep thinking mode as explicit option or triggered by specific conditions only
- [ ] Add logging to track when and why 💭 is used

### Phase 2: Context-Aware Follow-Up Generator (Priority: High)
- [ ] Create `FollowUpQuestionGenerator` class that analyzes actual conversation context
- [ ] Implement phrase matching against user inputs stored in knowledge base
- [ ] Generate questions using proper sentence structure with correct word ordering
- [ ] Add validation step to check if question makes sense before output

### Phase 3: Fallback Mechanism (Priority: Medium)
- [ ] If follow-up generation fails/creates nonsense, use generic friendly response instead
- [ ] Implement "I'm having trouble understanding" fallback when context is unclear
- [ ] Add user-friendly clarification prompts rather than confusing questions

## Acceptance Criteria
1. No more grammatically incorrect or nonsensical 💭 emoji questions
2. Follow-up questions are clear, coherent, and directly related to previous input
3. Bot can handle ambiguous inputs gracefully without generating confusing responses
4. All follow-ups pass simple grammar check (subject-verb agreement, proper sentence structure)

## Estimated Effort: 6-8 hours
## Dependencies: Plan 001 (context storage needed for meaningful follow-ups)
