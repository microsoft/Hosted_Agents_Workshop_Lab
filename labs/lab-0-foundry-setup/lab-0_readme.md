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
| A deployed chat model (e.g. `gpt-5.4-mini`) | Labs 0, 4, 5 | Check Foundry → Build → Deployments |
| GitHub account | Labs 3, 4 | — |
| Docker Desktop _(optional — cloud build available)_ | Lab 4 local builds only | `docker info` |

> **Tip for beginners:** Labs 0–3 only need .NET 10, Azure CLI, and a Foundry project. You do not need Docker or `azd` until Lab 4.

## Required Azure permissions

This workshop deploys through the **`azd ai agent`** pathway, which provisions a Foundry project and deploys your agent for you. Two different identities are involved — confirm you have the right roles **before Lab 4**.

### You (the person running the labs)

| Scope | Role | Why it's needed |
|---|---|---|
| Subscription or resource group | **Contributor** | `azd provision` creates the Foundry account, project, model deployment, container registry, App Insights, and Log Analytics |
| Subscription or resource group | **Role Based Access Control Administrator** (or **Owner**) | `azd deploy` assigns roles to the agent's managed identity (for example **AcrPull**). Without rights to create role assignments, deployment or the agent's first model call fails |
| Foundry project | **Azure AI Developer** | Create and update agent versions and model deployments in the project |

Sign in with both CLIs before Lab 4:

```powershell
az login
azd auth login
```

### The agent's managed identity (created automatically)

When your agent is deployed, Foundry gives it its own Microsoft Entra **managed identity**. That identity — not your account — calls the model at runtime, so it needs:

| Scope | Role | Why it's needed |
|---|---|---|
| Foundry account | **Cognitive Services OpenAI User** | Lets the running container call the model (`chat/completions`) |
| Foundry account | **Azure AI Developer** | Lets the container read the project's model connection |

`azd` grants the agent identity **AcrPull** automatically. If your first `azd ai agent invoke` returns `401 PermissionDenied`, grant the two roles above to the agent identity — see [Lab 4 troubleshooting](../lab-4-deploy/lab-4_readme.md#troubleshooting).

### Security best practices for hosted agents

- **Least privilege:** never give the agent's managed identity Owner or Contributor. Scope its roles to the **Foundry account/project**, not the whole subscription.
- **Separate identities:** keep your operator identity distinct from the agent's runtime identity.
- **Managed identity, not keys:** the agent authenticates with its Entra identity — no API keys are baked into the image or environment.
- **Pull-only registry access:** the agent needs **AcrPull** only, never push. Keep the admin user disabled on the container registry (the workshop infrastructure already does this).
- **Clean up:** run `azd down` when you finish to remove the resources and their role assignments.

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
   $env:MODEL_DEPLOYMENT_NAME = "gpt-5.4-mini"
   ```

   **macOS / Linux alternative:**

   ```bash
   export AZURE_AI_PROJECT_ENDPOINT="https://<resource>.services.ai.azure.com/api/projects/<project>"
   export MODEL_DEPLOYMENT_NAME="gpt-5.4-mini"
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