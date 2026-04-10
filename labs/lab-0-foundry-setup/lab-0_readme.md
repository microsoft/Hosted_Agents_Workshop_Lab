# Lab 0 — Core Guided: Set Up and Run a Hosted Agent

**Goal:** Open the repo in VS Code, install Python dependencies, configure Foundry environment variables, and validate the hosted agent locally.

**Time:** 25 minutes

**You will need:** Python 3.12+, Azure CLI, and access to a Microsoft Foundry project.

## Steps

1. Open the repository in VS Code.
2. Install [uv](https://docs.astral.sh/uv/getting-started/installation/) if you do not have it yet:

   **Windows (PowerShell):**

   ```powershell
   powershell -ExecutionPolicy ByPass -c "irm https://astral.sh/uv/install.ps1 | iex"
   ```

   **macOS / Linux:**

   ```bash
   curl -LsSf https://astral.sh/uv/install.sh | sh
   ```

3. Install dependencies (uv creates the virtual environment automatically):

   ```
   uv sync
   ```

4. Set the required environment variables:

   **Windows (PowerShell):**

   ```powershell
   $env:AZURE_AI_PROJECT_ENDPOINT = "https://<resource>.services.ai.azure.com/api/projects/<project>"
   $env:MODEL_DEPLOYMENT_NAME = "gpt-4.1-mini"
   ```

   **macOS / Linux:**

   ```bash
   export AZURE_AI_PROJECT_ENDPOINT="https://<resource>.services.ai.azure.com/api/projects/<project>"
   export MODEL_DEPLOYMENT_NAME="gpt-4.1-mini"
   ```

5. Run the hosted agent:

   ```
   uv run python src/workshop_lab_agent_host/main.py
   ```

6. In a second terminal, send a test request to the local `/responses` endpoint:

   **Windows (PowerShell):**

   ```powershell
   Invoke-RestMethod -Method Post `
       -Uri "http://localhost:8088/responses" `
       -ContentType "application/json" `
       -Body '{"input":"We need an internal agent with private API access and workflow orchestration. Should we start with a hosted agent?"}'
   ```

   **macOS / Linux:**

   ```bash
   curl -X POST http://localhost:8088/responses \
     -H "Content-Type: application/json" \
     -d '{"input":"We need an internal agent with private API access and workflow orchestration. Should we start with a hosted agent?"}'
   ```

7. Confirm that the agent answers as a Hosted Agent Readiness Coach.

**Expected result:** The hosted agent is reachable on `http://localhost:8088/responses` and responds with implementation guidance.