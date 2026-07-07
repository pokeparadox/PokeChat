# Fix: Name Confusion & Cross-Session Identity

## Problem
- User says "trains" when asked for name → bot accepts "trains" as name without validation
- Cross-session identity leak: user never said name "Kev" but bot uses it from previous session
- Greeting-as-name bug (Phase 23 fix may have regressed): "hi how are you" treated as name

## Root Cause
1. **Name input validation**: `HandleNameInput` accepts any single-word response as a name — no check for common nouns, game words, or known vocabulary
2. **Cross-session identity**: `_currentUserId` persists across sessions via user DB record; if first session set a garbled name, subsequent sessions inherit it
3. **Name vs topic ambiguity**: When user is asked for name and says "trains", there's no disambiguation — bot can't tell if it's a name or a topic shift

## Proposed Fixes

### 1. Name Validation
- After initial name extraction, check against POS dictionary — if it's a known noun/verb/adjective, ask for confirmation: "Did you mean your name is X, or are you talking about Y?"
- Reject one-word responses that are known common nouns (pizza, trains, software)
- Validate name length (>= 2 characters, <= 30 characters)

### 2. Cross-Session Identity Cleanup
- On session start, if user identity is restored from DB, verify with user: "Welcome back! Are you still [name]?"
- If user doesn't confirm, treat as new identity
- Reset `_currentUserId` when name confirmation fails

### 3. Greeting-as-Name Regression
- Verify that greeting word check runs before name assignment in `HandleNameInput`
- Add `greeting_words` DB lookup before accepting first-word-as-name

### 4. Summary Identity
- In `BuildSessionSummary`, use the name the user provided THIS session, not the DB-stored name from previous sessions

## Priority: Medium
