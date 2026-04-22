# Lab 0 — Core Guided: Set Up and Run a Hosted Agent

> **Progress:** Lab 0 of 5 — `[Lab 0] → Lab 1 → Lab 2 → Lab 3 → Lab 4 → Lab 5`

**Goal:** Open the repo in a dev container or Codespace, restore dependencies, configure Foundry environment variables, and validate the hosted agent locally.

**Time:** 25 minutes

**You will need:**

| Tool | Required for | Verify with |
|---|---|---|
| .NET 10 SDK | All labs | `dotnet --version` |
| Azure CLI | Labs 0, 4, 5 | `az version` |
| Azure Developer CLI (`azd`) | Lab 4 | `azd version` |
| Access to a Microsoft Foundry project | Labs 0, 4, 5 | Sign in at [ai.azure.com](https://ai.azure.com/) |
| A deployed chat model (e.g. `gpt-4.1-mini`) | Labs 0, 4, 5 | Check Foundry → Build → Deployments |
| GitHub account | Labs 3, 4 | — |
| Docker Desktop _(optional — cloud build available)_ | Lab 4 local builds only | `docker info` |

> **Tip for beginners:** Labs 0–3 only need .NET 10, Azure CLI, and a Foundry project. You do not need Docker or `azd` until Lab 4.

## Steps

1. Open the repository in VS Code or a Codespace.
2. Open `.devcontainer/devcontainer.json` and review the .NET 10 and Docker features.
3. Rebuild the container if prompted.
4. Restore and build the solution:

   ```powershell
   dotnet restore
   dotnet build
   ```

   > **Checkpoint:** Both commands should complete with `0 Error(s)`. If you see SDK errors, confirm .NET 10 is installed with `dotnet --version`.

5. Set the required environment variables:

   ```powershell
   $env:AZURE_AI_PROJECT_ENDPOINT = "https://<resource>.services.ai.azure.com/api/projects/<project>"
   $env:MODEL_DEPLOYMENT_NAME = "gpt-4.1-mini"
   ```

   **macOS / Linux alternative:**

   ```bash
   export AZURE_AI_PROJECT_ENDPOINT="https://<resource>.services.ai.azure.com/api/projects/<project>"
   export MODEL_DEPLOYMENT_NAME="gpt-4.1-mini"
   ```

   > **Where to find these values:** Open the [Foundry portal](https://ai.azure.com/), select your project, and copy the endpoint from the project overview page. The model deployment name is listed under **Build → Deployments**.

6. Run the hosted agent:

   ```powershell
   dotnet run --project src/WorkshopLab.AgentHost
   ```

   > **Checkpoint:** You should see `Now listening on: http://localhost:8088` in the terminal output.

7. In a second terminal, send a test request to the local `/responses` endpoint:

   ```powershell
   Invoke-RestMethod -Method Post `
       -Uri "http://localhost:8088/responses" `
       -ContentType "application/json" `
       -Body '{"input":"We need an internal agent with private API access and workflow orchestration. Should we start with a hosted agent?"}'
   ```

8. Confirm that the agent answers as a Hosted Agent Readiness Coach.

## Troubleshooting

| Symptom | Fix |
|---|---|
| `dotnet: command not found` | Install the .NET 10 SDK from [dot.net](https://dot.net/download) |
| `AuthenticationError` or `DefaultAzureCredential` failure | Run `az login` to sign in or refresh your session |
| `ResourceNotFound` | Verify your `AZURE_AI_PROJECT_ENDPOINT` matches the value in the Foundry portal |
| `DeploymentNotFound` | Check the deployment name in Foundry → Build → Deployments |
| Port 8088 already in use | Stop any other process using that port, then retry |

**Expected result:** The hosted agent is reachable on `http://localhost:8088/responses` and responds with implementation guidance.