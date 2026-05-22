# Context Tracking Integration Plan

## Current Gaps

| Gap | File | Detail |
|---|---|---|
| 1 | Core/ChatSession.ResolveSubject | 3rd-person pronouns ("he/him/his", "she/her", "they/them/their") pass through raw — never resolved to the tracked last subject |
| 2 | Core/ChatSession.ResolveObject | Only handles "it/this/that"; "him/her/them" in object position are ignored (ContextTracker.ResolvePronoun already handles them) |
| 3 | Responses/ResponseEngine.GenerateResponse | `_context` is stored but never read — `LastSubject`/`LastObject` from the just-processed sentence are available but unused |
| 4 | Core/ChatSession.Start | Bot's own response isn't stored in context, so future turns can't reference what was just said |
| 5 | Core/ChatSession | Context is never cleared; accumulates across the entire session |

## Steps

### Step 1 — Complete subject pronoun resolution (`Core/ChatSession.ResolveSubject`)
Add third-person routing so "he likes pizza" → resolves "he" to the last known subject:
```
"i/me/my/myself"  → userName           (existing)
"we/us/our"       → userName           (existing)
"he/him/his"      → _context.ResolvePronoun("he")
"she/her"         → _context.ResolvePronoun("she")
"they/them/their" → _context.ResolvePronoun("they")
else              → raw subject        (existing)
```

### Step 2 — Complete object pronoun resolution (`Core/ChatSession.ResolveObject`)
Extend dispatch so all pronoun types reach `_context.ResolvePronoun`:
```
Current:  "it/this/that" → ResolvePronoun, else → raw
Proposed: delegate ALL known pronoun tokens to ResolvePronoun:
          "it/this/that/him/her/them"
```

### Step 3 — Use context in `ResponseEngine.GenerateResponse`
Add a new response tier between "existing fact check" and "random user facts" that references `_context.LastSubject` / `_context.LastObject`:
```
1. pattern rule match            (existing)
2. existing fact check           (existing)
3. context-aware follow-up       ★ NEW — reference LastSubject/LastObject
4. random user fact follow-up    (existing)
5. default fallback              (existing)
```

### Step 4 — Store bot response in context (`Core/ChatSession.Start`)
After `response = ProcessInput(input)`, add:
```csharp
_context.SetContext("last_response", response);
```

### Step 5 — Add `_context.Clear()` on session boundaries
Call `_context.Clear()` in `HandleNameInput` before setting new user context (now that user is known, prior context is stale).
