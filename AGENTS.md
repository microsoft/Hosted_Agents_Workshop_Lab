# AGENTS.md

This file defines repository-level guidance for coding agents and contributors.

## Repository Purpose

This repo is a beginner workshop for Microsoft Foundry hosted agents with Python.

Primary learning flow:

1. Local hosted agent setup and validation
2. Copilot customization
3. Deterministic tool implementation and tests
4. CI validation
5. Azure deployment flow
6. UI integration and screenshot capture

## General Rules

- Keep instructions beginner-friendly and copy-paste ready.
- Prefer placeholder values in docs over real subscription, tenant, endpoint, or registry identifiers.
- Keep examples safe for public sharing.
- Preserve the current lab sequence and avoid reordering labs without a clear reason.

## Documentation Rules

- Use short sections and simple language.
- Include expected results for each lab step.
- For any command that depends on user environment, show placeholders:
  - `<account>`
  - `<project>`
  - `<your-acr-name>`
- If screenshots are referenced, ensure the files exist in `labs/lab-5-ui/images`.

## Code and Build Rules

- Python 3.12+ is the target runtime.
- Use `uv` for dependency management. Dependencies are declared in `pyproject.toml` and locked in `uv.lock`.
- Keep deterministic business logic in `src/workshop_lab_core`.
- Keep hosted agent host behavior in `src/workshop_lab_agent_host`.
- Run tests when changing core logic:

```powershell
uv run pytest tests/ -v
```

## UI Screenshot Workflow

When UI images need refresh for docs:

1. Start the app:

```powershell
uv run python src/workshop_lab_chat_ui/app.py
```

2. In another terminal, run screenshot capture:

```powershell
npm run capture:screenshots
```

Expected outputs:

- `labs/lab-5-ui/images/01-chat-ui-landing.png`
- `labs/lab-5-ui/images/02-chat-ui-prompt-entered.png`
- `labs/lab-5-ui/images/03-chat-ui-response-hd.png`

## Public Sharing Checklist

Before publishing updates:

- No hardcoded personal tenant IDs, subscription IDs, or project endpoints
- No hardcoded personal ACR names
- `.gitignore` includes local-only artifacts (for example `node_modules/`, `.venv/`, `__pycache__/`, local logs, debug outputs)
- Beginner docs still read top-to-bottom without hidden assumptions
