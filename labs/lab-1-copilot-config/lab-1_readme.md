# Lab 1 — Core Guided: Add Copilot Instructions for Hosted Agents

**Goal:** Create repository-specific Copilot instructions and a skill that improves Hosted Agent design reviews.

**Time:** 30 minutes

**You will need:** Lab 0 completed.

## Steps

1. Create `.github/copilot-instructions.md`.
2. Add a **Language** section stating that the repo uses Python and Microsoft Foundry Hosted Agents.
3. Add a **Code style** section asking Copilot to prefer deterministic local tools for domain logic and small, testable classes.
4. Add a **Testing** section telling Copilot to suggest pytest coverage for every deterministic tool change.
5. Add a line asking Copilot to keep answers concise and operational.
6. Create `.github/skills/hosted-agent-review/SKILL.md`.
7. Add YAML frontmatter with `name: hosted-agent-review` and a description about reviewing agent.yaml, Dockerfile, `/responses` compatibility, Linux AMD64 builds, and Foundry readiness.
8. In the skill body, instruct Copilot to review:
   - environment variables
   - container entrypoint
   - hosted-agent protocol settings
   - local validation steps
   - deployment risks
9. Test the configuration by asking Copilot to review `src/workshop_lab_agent_host/agent.yaml`.

**Expected result:** Copilot responds with Hosted Agent-aware guidance shaped by your repo instructions and skill.