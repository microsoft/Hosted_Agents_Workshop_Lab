# Lab 1 — Core Guided: Add Copilot Instructions for Hosted Agents

> **Progress:** Lab 1 of 5 — `Lab 0 → [Lab 1] → Lab 2 → Lab 3 → Lab 4 → Lab 5`

**Goal:** Create repository-specific Copilot instructions and a skill that improves Hosted Agent design reviews.

**Time:** 30 minutes

**You will need:** Lab 0 completed.

## Part 1 — Repository Copilot instructions

Copilot automatically loads `.github/copilot-instructions.md` for every chat in this repo, so it's the fastest way to shape its answers.

1. Create `.github/copilot-instructions.md`.
2. Use the starter below and adjust the `TODO` lines for your project. It already covers **Language**, **Code style**, **Testing**, and a concise **Response style**:

```markdown
# Copilot instructions — WorkshopLab hosted agent

## Language & stack
- This repository uses **.NET 10** and **Microsoft Foundry hosted agents**.
- TODO: note anything else specific to your project.

## Code style
- Prefer **deterministic local tools** (small, testable classes) for domain logic instead of free-form model calls.
- Keep public APIs minimal and clearly named.

## Testing
- For every change to a deterministic tool, suggest matching **xUnit** test coverage.

## Response style
- Keep answers concise and operational — commands, file paths, and next steps over long prose.
```

> **Checkpoint:** Open a Copilot Chat panel and ask: *"What testing framework does this repo use?"* Copilot should reference **xUnit** because your `copilot-instructions.md` mentions it. If Copilot gives a generic answer, reopen VS Code to reload the instructions file.

## Part 2 — Hosted-agent review skill

A skill packages focused review guidance Copilot can apply on demand.

3. Create `.github/skills/hosted-agent-review/SKILL.md` using this starter (the YAML frontmatter is required — `name` and `description`):

```markdown
---
name: hosted-agent-review
description: Review a Foundry hosted agent for readiness — agent.yaml, Dockerfile, the /responses protocol contract, Linux AMD64 builds, and Foundry deployment risks.
---

# Hosted agent review

When asked to review a hosted agent, check:

- **Environment variables** — required values come from config/env, not hardcoded.
- **Container entrypoint** — the Dockerfile builds from the correct context and starts the app.
- **Hosted-agent protocol** — the `/responses` endpoint and `Foundry-Features` header match the contract.
- **Local validation** — the steps to run and smoke-test before deploying.
- **Deployment risks** — `linux/amd64` image, RBAC/permissions, and rollback.
```

4. Test the configuration by asking Copilot to review `src/WorkshopLab.AgentHost/agent.yaml`.

**Expected result:** Copilot responds with Hosted Agent-aware guidance shaped by your repo instructions and skill.

---

**Navigation:** [◀ Lab 0 — Foundry setup](../lab-0-foundry-setup/lab-0_readme.md) · [📚 All labs](../README.md) · [Next: Lab 2 — Implementation shape ▶](../lab-2-implementation-shape/lab-2_readme.md)