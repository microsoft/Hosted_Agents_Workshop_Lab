# Lab 2 — Core Guided: Improve a Local Hosted Agent Tool

**Goal:** Extend the hosted agent by improving one deterministic local tool and verifying the change through tests and a local `/responses` call.

**Time:** 20 minutes

**You will need:** Lab 1 completed.

## Steps

1. Open `src/workshop_lab_core/hosted_agent_advisor.py`.
2. Review the existing tools:
   - `recommend_implementation_shape`
   - `build_launch_checklist`
   - `troubleshoot_hosted_agent`
3. Pick one tool to improve. Suggested option: add a stronger recommendation path for scenarios that require custom code, tool access, and workflow orchestration at the same time.
4. Update the deterministic logic in `HostedAgentAdvisor`.
5. Add or update tests in `tests/test_hosted_agent_advisor.py`.
6. Run:

   ```powershell
   uv run pytest tests/ -v
   ```

7. Start the hosted agent locally.
8. Send a request to `/responses` that exercises your updated tool logic.
9. Confirm the response matches the new deterministic behavior.

**Expected result:** A real feature change lands in the hosted agent and is covered by tests.