---
name: hosted-agent-review
description: >
  Review agent.yaml, Dockerfile, and source code for Microsoft Foundry Hosted Agent readiness.
  Checks /responses endpoint compatibility, Linux AMD64 container builds, and Foundry deployment requirements.
---

When reviewing a hosted agent project, check the following areas:

## Environment Variables

- Verify `AZURE_AI_PROJECT_ENDPOINT` and `MODEL_DEPLOYMENT_NAME` are declared in `agent.yaml` and referenced in the application code.
- Confirm no secrets or real endpoint values are hardcoded.

## Container Entrypoint

- The Dockerfile must expose port **8088**.
- The entrypoint should start the FastAPI server (`uvicorn` or `python -m`).
- The image must build for **linux/amd64**.

## Hosted-Agent Protocol Settings

- `agent.yaml` must declare `kind: hosted`.
- The `protocols` section must include `protocol: responses` at `version: v1`.
- The `/responses` endpoint must accept POST requests with JSON input and return the Foundry-compatible response format.

## Local Validation Steps

- The agent should run locally on `http://localhost:8088/responses` before any deployment.
- Recommend sending a test POST request to verify the response structure.
- Tests for deterministic tools in `workshop_lab_core` should pass (`uv run pytest tests/ -v`).

## Deployment Risks

- Flag any missing environment variable declarations.
- Warn if the Dockerfile does not run as a non-root user.
- Check that the build context includes both `workshop_lab_core` and `workshop_lab_agent_host`.
- Confirm the container image tag strategy avoids using `latest` in production.
