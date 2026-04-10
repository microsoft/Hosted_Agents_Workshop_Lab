# Copilot Instructions

## Language

This repository uses **Python 3.12+** and builds a **Microsoft Foundry Hosted Agent**.
The agent exposes the `/responses` endpoint on port 8088 using FastAPI.
Dependencies are managed with `uv` (`pyproject.toml` + `uv.lock`).

## Code Style

- Keep domain logic deterministic and free of network calls. Place it in `src/workshop_lab_core/`.
- Prefer small, pure functions that are easy to unit-test over large classes.
- Use type hints on all public function signatures.
- Avoid adding abstractions, helpers, or error handling beyond what the caller needs.

## Testing

- Suggest `pytest` test coverage for every change to a deterministic tool in `workshop_lab_core`.
- Tests live in `tests/` and run with `uv run pytest tests/ -v`.
- Use `@pytest.mark.parametrize` for multi-case assertions instead of duplicating test functions.
- Keep test code concise — one assert per behavior, no unnecessary setup.
