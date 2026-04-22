# Lab 4 — Core Guided: Provision Azure, Publish The Image, And Deploy The Hosted Agent

> **Progress:** Lab 4 of 5 — `Lab 0 → Lab 1 → Lab 2 → Lab 3 → [Lab 4] → Lab 5`

**Goal:** Follow the hosted-agent deployment path end to end: verify prerequisites, provision Azure resources, build and publish the container image to ACR, deploy the hosted agent to Foundry, and verify it responds.

**Time:** 35 minutes

**You will need:** Lab 3 completed.

**Reference:** [Microsoft Learn — Quickstart: Deploy your first hosted agent (azd)](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?pivots=azd)

---

## Prerequisites

Confirm these requirements before starting:

| Requirement | Verify with | Notes |
|---|---|---|
| azd version 1.23.0 or later | `azd version` | Update: `winget upgrade Microsoft.Azd` |
| Azure CLI authenticated | `az login` | — |
| azd authenticated | `azd auth login` | — |
| Contributor access on your Azure subscription | — | Required for `azd provision` |
| Docker Desktop _(optional)_ | `docker info` | Only needed for local container builds. This lab uses `az acr build` (cloud build) by default, so Docker Desktop is **not required**. |

---

## Step 1: Verify Prerequisites

1. Check your azd version:

   ```powershell
   azd version
   ```

   If the version is below 1.23.0, update it:

   ```powershell
   winget upgrade Microsoft.Azd
   ```

2. _(Optional)_ Verify Docker Desktop is running:

   ```powershell
   docker info
   ```

   Docker Desktop is only needed if you want to build images locally. This lab uses `az acr build` (cloud build) by default, so you can skip this step if Docker is not available on your machine.

3. Log in to Azure:

   ```powershell
   az login
   azd auth login
   ```

---

## Step 2: Review the Agent Definition

1. Open `src/WorkshopLab.AgentHost/agent.yaml` and confirm:
   - `kind: hosted`
   - `protocol: responses` at version `v1`
   - Environment variables `AZURE_AI_PROJECT_ENDPOINT` and `MODEL_DEPLOYMENT_NAME` are listed

2. Open `src/WorkshopLab.AgentHost/Dockerfile` and confirm the multi-stage build uses the .NET 10 Alpine images and targets `linux/amd64`.

---

## Step 3: Configure the azd Environment

If you do not already have an azd environment, create one now:

```powershell
azd env new <environment-name>
```

> **Note:** The environment name determines your resource group name (`rg-<environment-name>`). Choose a unique name to avoid conflicts with existing resource groups in your subscription.

Set the required environment values. Replace the placeholders with your Foundry project endpoint and model deployment name:

```powershell
azd env set AZURE_AI_PROJECT_ENDPOINT "https://<resource>.services.ai.azure.com/api/projects/<project>"
azd env set MODEL_DEPLOYMENT_NAME "gpt-4.1-mini"
```

> **Note:** `azd env set` works the same on all platforms. If you also need shell-level variables for local testing, use `export` on macOS/Linux:
>
> ```bash
> export AZURE_AI_PROJECT_ENDPOINT="https://<resource>.services.ai.azure.com/api/projects/<project>"
> export MODEL_DEPLOYMENT_NAME="gpt-4.1-mini"
> ```

---

## Step 4: Provision Azure Resources

Preview then provision the required Azure resources:

```powershell
azd provision --preview
azd provision
```

> **Note:** You need Contributor access on your Azure subscription for resource provisioning. Provisioning takes approximately 2–3 minutes and creates the Azure Container Registry.

> **Estimated Azure costs:** The workshop provisions an Azure Container Registry (Standard SKU). At typical pay-as-you-go pricing this costs roughly **$0.17/day (~$5/month)**. The hosted agent container runs only while started and is billed per-second of compute. For a short workshop session, expect **under $1** in total ACR + compute costs. Run `azd down` after the workshop to stop all charges. See the [Azure Container Registry pricing page](https://azure.microsoft.com/pricing/details/container-registry/) for current rates.

After provisioning, capture the output values:

```powershell
azd env get-values
```

Confirm the following values are present:

- `AZURE_CONTAINER_REGISTRY_NAME`
- `AZURE_CONTAINER_REGISTRY_ENDPOINT`
- `AZURE_AI_PROJECT_ENDPOINT`
- `MODEL_DEPLOYMENT_NAME`

> **Checkpoint:** If all four values appear in the output, provisioning succeeded. If `azd provision` failed with `SkuNotSupported` or a region error, see the [Known Issues](../../knownissues.md) for workarounds.

---

## Step 5: Test the Agent Locally

Before publishing to Azure, confirm the agent works in your local environment.

1. Set the required environment variables for your session:

   ```powershell
   $env:AZURE_AI_PROJECT_ENDPOINT = "https://<resource>.services.ai.azure.com/api/projects/<project>"
   $env:MODEL_DEPLOYMENT_NAME = "gpt-4.1-mini"
   ```

   **macOS / Linux alternative:**

   ```bash
   export AZURE_AI_PROJECT_ENDPOINT="https://<resource>.services.ai.azure.com/api/projects/<project>"
   export MODEL_DEPLOYMENT_NAME="gpt-4.1-mini"
   ```

2. Run the hosted agent:

   ```powershell
   dotnet run --project src/WorkshopLab.AgentHost
   ```

3. In a second terminal, send a test request:

   ```powershell
   Invoke-RestMethod -Method Post `
       -Uri "http://localhost:8088/responses" `
       -ContentType "application/json" `
       -Body '{"input":"Should we use a hosted agent for our team onboarding workflow?"}'
   ```

   You should receive a structured recommendation from the Readiness Coach.

   Alternatively, open `src/WorkshopLab.AgentHost/run-requests.http` and use the VS Code REST Client extension to send the pre-built requests.

4. Stop the local server with **Ctrl+C**.

If the agent fails to start, check:

| Symptom | Fix |
|---|---|
| `AuthenticationError` or `DefaultAzureCredential` failure | Run `az login` again to refresh your session |
| `ResourceNotFound` | Verify your endpoint URL matches the value in the Foundry portal |
| `DeploymentNotFound` | Check the deployment name in Foundry → Build → Deployments |
| Port 8088 already in use | Stop any other process using this port |

---

## Step 6: Build and Publish the Container Image

Build the agent container and push it to ACR using ACR cloud build (no local daemon push required).

> **Important:** The build context must be `./src` (not just `./src/WorkshopLab.AgentHost`) because the Dockerfile copies both `WorkshopLab.Core` and `WorkshopLab.AgentHost`. Use `--file` to point to the Dockerfile.

```powershell
$acrName = (azd env get-values | Select-String "AZURE_CONTAINER_REGISTRY_NAME").ToString().Split("=")[1].Trim('"')
az acr build --registry $acrName --image workshoplab-agent:lab4 --platform linux/amd64 `
    --file ./src/WorkshopLab.AgentHost/Dockerfile `
    ./src
```

> **Note:** The first build takes 3–5 minutes as the base .NET 10 images are downloaded. Subsequent builds are faster.

When the build completes, note the full image URI shown in the output:

```
<acr-name>.azurecr.io/workshoplab-agent:lab4
```

> **Checkpoint:** The ACR build output should end with a line like `Run ID: ca1 was successful after ...`. If the build fails with a missing project reference, verify you are using `./src` as the build context — not `./src/WorkshopLab.AgentHost`. See [Known Issues — Issue 3](../../knownissues.md) for details.

---

## Step 7: Deploy the Hosted Agent to Foundry

Deploy the hosted agent definition to your Foundry project using the direct deployment script. Replace `<acr-name>` with the name captured in Step 6:

```powershell
./scripts/deploy-hosted-agent-direct.ps1 `
    -ImageUri "<acr-name>.azurecr.io/workshoplab-agent:lab4"
```

The script creates or updates the hosted agent definition in Foundry with:
- The container image URI
- CPU and memory resource allocation
- Protocol version (`responses` v1)
- The required environment variables

> **Alternative — manifest-based deploy:** If you prefer to use the declarative `agent.yaml` manifest, run `./scripts/deploy-foundry-agent.ps1` instead. Note that the manifest does not include an image URI; the Foundry control plane must be configured separately with the image.

---

## Step 8: Start the Hosted Agent Container

After the agent definition is registered, start the container using the Azure CLI:

```powershell
$accountName = "<resource-account-name>"   # e.g. my-foundry-account
$projectName = "<foundry-project-name>"    # e.g. my-foundry-project

az cognitiveservices agent start `
    --account-name $accountName `
    --project-name $projectName `
    --name hosted-agent-readiness-coach `
    --agent-version 1 `
    --show-logs
```

The `--show-logs` flag streams the container startup logs in real-time. Watch for:
- `Now listening on: http://[::]:8088` — application started
- `GET /readiness → 200 OK` — health probe passing
- `Agent deployment is now running` — container reached Running state

**Option B — Foundry portal:**

1. Open the [Foundry portal](https://ai.azure.com/) and sign in.
2. Select your project → **Build** → **Agents**.
3. Find `hosted-agent-readiness-coach`, open its details page, and select **Start**.

**Option C — GitHub Copilot with Foundry MCP:**

If the Foundry MCP tools are authenticated to the correct tenant, ask Copilot:

> "Start the hosted agent container for `hosted-agent-readiness-coach` in my Foundry project"

> **Tip:** To check the running status after start: `az cognitiveservices agent status --account-name $accountName --project-name $projectName --name hosted-agent-readiness-coach --agent-version 1`

> **Checkpoint:** The status command should return `provisioningState: Running` and `health_state: Healthy`. If you see a different state, wait 1–2 minutes and check again. If the container fails to start, see [Known Issues — Issue 6](../../knownissues.md) for the correct CLI commands.

---

## Step 9: Verify and Test the Deployed Agent

Poll the agent status until it shows **Running** (typically 1–2 minutes).

Use this validation prompt for all verification paths in this step:

> `We are onboarding a team to Microsoft Foundry hosted agents. We need private API access, a repeatable deployment process, and a launch checklist for production readiness. Should we use a hosted agent for this scenario, and what implementation shape do you recommend?`

### Verify in the Foundry portal

1. Open the [Foundry portal](https://ai.azure.com/) and sign in.
2. Select your project and go to **Build** → **Agents**.
3. Find `hosted-agent-readiness-coach` and confirm the status is **Running**.
4. Select **Open in playground** in the top toolbar.
5. In the chat interface, paste the validation prompt:

   > `We are onboarding a team to Microsoft Foundry hosted agents. We need private API access, a repeatable deployment process, and a launch checklist for production readiness. Should we use a hosted agent for this scenario, and what implementation shape do you recommend?`

6. Verify the agent response includes:
   - a recommendation on whether a hosted agent fits the scenario
   - a suggested implementation shape or deployment approach
   - operational guidance such as rollout steps, prerequisites, or a checklist

### Verify via VS Code REST Client

1. Open `src/WorkshopLab.AgentHost/run-requests.http`.
2. Go to the **Production requests via Foundry Agent Service** section.
3. Use the first request, which is prefilled with the validation prompt.
4. If your REST client requires a token variable, acquire one first:

   ```powershell
   az account get-access-token --resource "https://ai.azure.com" --query accessToken -o tsv
   ```

5. Send the request to `POST {{projectEndpoint}}/openai/v1/responses`.
6. Confirm the request succeeds and the response content addresses the scenario in the validation prompt.

### Verify via REST API

Hosted agents are invoked through the OpenAI Responses API with an `agent_reference`. Obtain a bearer token and POST to the project-level endpoint:

```powershell
$projectEndpoint = "https://<account>.services.ai.azure.com/api/projects/<project>"
$tok = (az account get-access-token --resource "https://ai.azure.com" --query accessToken -o tsv)

$payload = @{
   input = @(@{ role = "user"; content = "We are onboarding a team to Microsoft Foundry hosted agents. We need private API access, a repeatable deployment process, and a launch checklist for production readiness. Should we use a hosted agent for this scenario, and what implementation shape do you recommend?" })
    agent_reference = @{ name = "hosted-agent-readiness-coach"; type = "agent_reference" }
} | ConvertTo-Json -Depth 5

Invoke-RestMethod -Method Post `
    -Uri "$projectEndpoint/openai/v1/responses" `
    -Headers @{ "Authorization" = "Bearer $tok"; "Content-Type" = "application/json"; "api-version" = "2025-01-01-preview" } `
    -Body $payload
```

Validation checks:
- the HTTP request succeeds without a 4xx or 5xx response
- the response object status is `completed`
- the answer includes both a hosted-agent recommendation and implementation guidance

You can also use the pre-built production requests in `src/WorkshopLab.AgentHost/run-requests.http`.

---

## Clean Up Resources

> **Warning:** This permanently deletes the ACR and all resources created by `azd provision`. Run `azd down --preview` first to see what will be deleted.

```powershell
azd down --preview
azd down
```

The Foundry project and hosted agent definition remain unless deleted separately in the Foundry portal.

---

## Troubleshooting

| Problem | Solution |
|---|---|
| Docker build errors | Ensure Docker Desktop is running: `docker info` — or skip local builds and use `az acr build` (cloud build) |
| ACR build fails with missing project reference | Use `./src` as build context, not `./src/WorkshopLab.AgentHost`. See [Known Issues — Issue 3](../../knownissues.md) |
| `SubscriptionNotRegistered` error | Register providers: `az provider register --namespace Microsoft.CognitiveServices` |
| `AuthorizationFailed` during provisioning | Request Contributor role on your subscription or resource group |
| `SkuNotSupported` during ACR provisioning | Try a different region or reuse an existing ACR. See [Known Issues — Issue 2](../../knownissues.md) |
| Agent doesn't start locally | Verify environment variables are set and run `az login` to refresh credentials |
| `AcrPullUnauthorized` error | Grant AcrPull role to the project's managed identity on the container registry |
| Container start returns 404 | Use `az cognitiveservices agent start` (Azure CLI ≥ 2.80). See [Known Issues — Issue 6](../../knownissues.md) |
| azd version too old | Run `winget upgrade Microsoft.Azd` (Windows) or `brew upgrade azd` (macOS) |
| Model not found | Verify the deployment name in Foundry → Build → Deployments matches `MODEL_DEPLOYMENT_NAME` |
| `azd provision` fails with existing resource group | Choose a unique environment name or delete the existing resource group first |

> **Full list of documented issues and workarounds:** See [knownissues.md](../../knownissues.md).

---

## What This Lab Demonstrates

This lab follows the [Microsoft Learn hosted-agent quickstart](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?pivots=azd) adapted for the .NET Agent Framework:

- Prerequisite verification before deployment
- Azure provisioning through `azd` and Bicep (ACR)
- Local agent testing before cloud deployment
- Cloud image publishing to Azure Container Registry via `az acr build`
- Hosted-agent registration in Foundry via the deployment script
- Container start and running-state verification
- End-to-end validation via the Foundry playground and REST API

---

## Expected Result

The `hosted-agent-readiness-coach` agent is deployed and running in Foundry Agent Service. It responds to requests in the Foundry playground and through the project-level `openai/v1/responses` API with implementation shape recommendations, launch checklists, and troubleshooting guidance.