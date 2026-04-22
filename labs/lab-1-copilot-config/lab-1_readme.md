# Lab 1 — Core Guided: Add Copilot Instructions for Hosted Agents

> **Progress:** Lab 1 of 5 — `Lab 0 → [Lab 1] → Lab 2 → Lab 3 → Lab 4 → Lab 5`

**Goal:** Create repository-specific Copilot instructions and a skill that improves Hosted Agent design reviews.

**Time:** 30 minutes

**You will need:** Lab 0 completed.

## Steps

1. Create `.github/copilot-instructions.md`.
2. Add a **Language** section stating that the repo uses .NET 10 and Microsoft Foundry Hosted Agents.
3. Add a **Code style** section asking Copilot to prefer deterministic local tools for domain logic and small, testable classes.
4. Add a **Testing** section telling Copilot to suggest xUnit coverage for every deterministic tool change.
5. Add a line asking Copilot to keep answers concise and operational.
6. Create `.github/skills/hosted-agent-review/SKILL.md`.

> **Checkpoint:** Open a Copilot Chat panel and ask: *"What testing framework does this repo use?"* Copilot should reference **xUnit** because your `copilot-instructions.md` mentions it. If Copilot gives a generic answer, reopen VS Code to reload the instructions file.

7. Add YAML frontmatter with `name: hosted-agent-review` and a description about reviewing agent.yaml, Dockerfile, `/responses` compatibility, Linux AMD64 builds, and Foundry readiness.
8. In the skill body, instruct Copilot to review:
   - environment variables
   - container entrypoint
   - hosted-agent protocol settings
   - local validation steps
   - deployment risks
9. Test the configuration by asking Copilot to review `src/WorkshopLab.AgentHost/agent.yaml`.

**Expected result:** Copilot responds with Hosted Agent-aware guidance shaped by your repo instructions and skill.