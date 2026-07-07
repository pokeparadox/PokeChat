# Plan 003: Add Self-Awareness Capability

## Problem Description
- Bot fails to recognize when users point out problems with its responses
- Session 1 Turn 6: User says "That did not make sense!" → bot responds "Good to know! I've stored that away" as if nothing happened
- When user asks about the confusing question, bot doesn't acknowledge it was actually a bad response
- Bot should recognize and apologize when users indicate something went wrong

## Root Cause Analysis
1. No self-reflection or meta-cognition capability in current architecture
2. Responses are purely reactive without monitoring conversation quality indicators
3. User feedback signals (confusion, frustration) not detected or prioritized
4. Missing "apology/acknowledgment" response pattern for user complaints

## Implementation Plan

### Phase 1: User Feedback Detection (Priority: High)
- [ ] Create `SentimentAnalyzer` class to detect negative emotions in user input
- [ ] Detect keywords/phrases indicating confusion or frustration ("doesn't make sense", "confusing", "mocking me")
- [ ] Track repeated complaints from same user about bot behavior

### Phase 2: Self-Aware Response Pattern (Priority: High)
- [ ] Implement `SelfAwareResponseHandler` to handle detected feedback issues
- [ ] Create apology/acknowledgment templates for different issue types
- [ ] Add "I apologize" pattern when users indicate problems with responses
- [ ] Include follow-up question after acknowledging the mistake

### Phase 3: Learning from Feedback (Priority: Medium)
- [ ] Store user feedback in knowledge base as learnable patterns
- [ ] Use feedback to improve future response generation
- [ ] Add "I'll try to do better" commitment pattern for repeated issues

## Acceptance Criteria
1. Bot acknowledges when users say responses didn't make sense
2. Apology generated before attempting new action after user complaint
3. No more pretending to store information that wasn't actually understood
4. User frustration decreases in subsequent interactions

## Estimated Effort: 5-7 hours
## Dependencies: Plan 001 (context storage needed for learning from feedback)
