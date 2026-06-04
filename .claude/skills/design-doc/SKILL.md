---
name: design-doc
description: This skill should be used when the user asks for a "design doc", says "let's design", "help me think through", "write a spec for", "design a feature", or makes any request to plan and document a new feature, system, or significant change before building it. Produces a three-layer design document: problem statement, functional spec, and technical spec.
version: 1.0.0
---

# Design Doc Skill

Conduct a structured design interview as a senior engineer and product thinker.
The goal: squeeze the user dry — surface assumptions, edge cases, tradeoffs, and things
they haven't thought about yet — before writing a single word of the doc.

The doc is a three-layer onion:
1. **Problem statement** — why does this exist?
2. **Functional spec** — what does it do?
3. **Technical spec** — how does it work?

Each layer has its own interview phase. Do not skip ahead.

---

## Phase 1 — Problem Statement Interview

**Goal:** Understand the problem deeply before any solution is discussed.

Interview the user with the `AskUserQuestion` tool. Cover:
- Who is affected by this problem and how often?
- What does the user do today without this feature? What's the workaround?
- What does success look like — how would we know this solved the problem?
- What's the cost of not solving it?
- Is this problem well-understood or are we still exploring?
- Are there constraints (regulatory, contractual, existing commitments) that bound the solution?
- Who are the stakeholders and do they agree on the problem?

**Rules:**
- Do not ask obvious questions. If the user said "we need a CSV export", don't ask "what format?" —
  ask "who consumes this export downstream and what do they do with it?" instead.
- Ask one focused batch of questions at a time using `AskUserQuestion` — not a wall of text.
- Keep going until a crisp, unambiguous problem statement can be written. Push back if it's vague.
- Do not move to Phase 2 until the problem statement can be written confidently.

**Output — write this before Phase 2:**

```markdown
## Problem Statement

**Context:** [who, what situation, what pain]
**Problem:** [specific, measurable problem being solved]
**Success criteria:** [how we know it's solved]
**Out of scope:** [explicitly what this does not solve]
```

---

## Phase 2 — Functional Spec Interview

**Goal:** Define what the feature does from the user's perspective — no implementation yet.

Interview the user with `AskUserQuestion`. Cover:
- Walk through the user journey step by step — what does the user do, click, see?
- What are the different user roles involved and do they have different experiences?
- What are the happy path and the failure paths?
- What happens at the boundaries — empty state, max limits, concurrent actions?
- Are there notifications, emails, or side effects?
- What are the permissions — who can do this, who can't?
- Are there any states this feature can be in (draft, active, archived, etc.)?
- What does the user see if something goes wrong?

**Rules:**
- Stay in user-land. "The user sees a table" is fine. "We'll use a JOIN" is not.
- Push on edge cases the user hasn't mentioned. If they describe a happy path, ask what breaks it.
- If requirements are ambiguous, make them pick — don't leave both options open.
- Do not move to Phase 3 until every user-facing behaviour is nailed down.

**Output — write this before Phase 3:**

```markdown
## Functional Spec

### User stories
- As a [role], I can [action] so that [outcome]

### User journeys
[Step-by-step for each key flow]

### Permissions
[Who can do what]

### Edge cases & failure states
[Empty states, limits, errors, concurrent actions]

### Out of scope
[Explicitly what this does not cover]
```

---

## Phase 3 — Technical Spec Interview

**Goal:** Define how it works — data model, API, frontend architecture, tradeoffs.

Interview the user with `AskUserQuestion`. Cover:

**Data**
- What new data needs to be stored? What's the shape?
- Does this touch existing tables — and if so, are migrations safe (backwards compatible)?
- Does `organization_id` RLS apply? Are there edge cases where a user accesses data across orgs?
- What are the data retention or deletion requirements?

**API & backend**
- Which tier handles this — Supabase direct, Edge Function, or Spring Boot?
- Are there performance concerns — large datasets, expensive queries, background jobs?
- Does this need to be transactional? What happens on partial failure?

**Frontend**
- Which existing components can be reused vs. what needs to be built new?
- Is there optimistic UI, or does the user wait for confirmation?
- What loading, error, and empty states need designing?

**Cross-cutting**
- Does this need audit logging (`withAuditLog()`)?
- Are there i18n implications — new translation keys across all locales?
- Are there any security concerns specific to this feature?
- What's the rollout plan — feature flag, phased, all at once?
- What could go wrong in production and how do we detect it?

**Rules:**
- Challenge assumptions. If they say "we'll just add a column", ask if that breaks RLS or
  requires a migration with backfill.
- Surface tradeoffs explicitly — don't just accept the first answer.
- Flag anything that touches auto-generated files (e.g. `supabase/types.ts`) — these must not
  be edited directly.

**Output — write the final doc:**

```markdown
## Technical Spec

### Data model
[New tables, columns, migrations — with RLS notes]

### API design
[Endpoints or Supabase calls — tier, method, path, request/response shape]

### Frontend architecture
[Components, hooks, state management approach]

### Audit & compliance
[withAuditLog usage, i18n keys, security notes]

### Rollout
[Feature flag, migration strategy, monitoring]

### Open questions
[Anything unresolved that needs a decision]
```

---

## Final output

Assemble all three layers into a single markdown file at:

```
docs/design/<feature-name>.md
```

Structure:
1. Problem Statement
2. Functional Spec
3. Technical Spec
4. Open Questions (anything that couldn't be resolved in the interview)

---

## Conduct rules

- **Never wing it.** If there isn't enough information to write a section confidently, ask more.
- **Non-obvious questions only.** The user knows their feature — probe what they haven't considered.
- **One phase at a time.** Complete and write each layer before starting the next interview.
- **Push back.** If an answer is vague, contradictory, or incomplete, say so and ask again.
- **Name the tradeoffs.** Every significant technical decision should have its alternative noted.
