# Fix: SVO Garbage Extraction → Broken Follow-ups

## Problem
- Bot generates broken questions like "Tell me more about that the energy that they and is converted with little waste as heat"
- "What else can you share about not and much sense?" (function words as objects)
- `SanitiseFollowUpPhrase` (Phase NN) masks symptoms but root cause is SVO extractor producing garbage triples

## Root Cause
1. **Compound predicate capture**: "are efficient and are on rails" → SVO extracts fragment triples from within the compound structure
2. **Relative clause leakage**: "the energy that they and" — clauses starting with "that/which/who" bleed into object extraction
3. **Function word objects**: "not", "much", "and" extracted as objects despite `FunctionWords` filter (filter only applies to single-word objects of `General` type)
4. **Pronoun resolution drift**: after several turns, `LastSubject`/`LastObject` become stale fragments from earlier garbled extractions

## Proposed Fixes

### 1. Strengthen Garbage Triple Detection
- Expand `FunctionWords` filter to apply to ALL predicate types, not just `General`
- Add clause-marker filter (`that`, `which`, `who`, `when`, `where`) — if object contains these, mark triple as garbage
- Reject triples where object length > 8 words (likely compound/run-on extraction)

### 2. Improve SVO Extraction
- Add max-object-length guard in `SvoExtractor.Extract()` or `ChatSession.ProcessSentence()`
- Split compound predicates at conjunction boundaries before SVO extraction
- Filter triples where object is entirely stop words

### 3. Add SanitiseFollowUpPhrase for Subject Too
- Currently only sanitises `LastObject` — also sanitise `LastSubject`
- If either sanitises to null, skip the follow-up turn entirely

### 4. Contextual Fallback
- If both `LastSubject` and `LastObject` fail sanitisation, generate a generic engagement response instead of a broken question

## Priority: High
