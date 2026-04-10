# Lab 4 — Core Guided: Provision Azure, Publish The Image, And Deploy The Hosted Agent

**Goal:** Follow the hosted-agent deployment path end to end: verify prerequisites, provision Azure resources, build and publish the container image to ACR, deploy the hosted agent to Foundry, and verify it responds.

**Time:** 35 minutes

**You will need:** Lab 3 completed.

**Reference:** [Microsoft Learn — Quickstart: Deploy your first hosted agent (azd)](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?pivots=azd)

---

## Prerequisites

Confirm these requirements before starting:

| Requirement | Verify with |
|---|---|
| azd version 1.23.0 or later | `azd version` |
| Docker Desktop installed and running | `docker info` |
| Azure CLI authenticated | `az login` |
| azd authenticated | `azd auth login` |
| Contributor access on your Azure subscription | Required for `azd provision` |

---

## Step 1: Verify Prerequisites

1. Check your azd version:

   ```powershell
   azd version
   ```

   If the version is below 1.23.0, update it:

   **Windows:**

   ```powershell
   winget upgrade Microsoft.Azd
   ```

   **macOS:**

   ```bash
   brew upgrade azd
   ```

   **Linux:**

   ```bash
   curl -fsSL https://aka.ms/install-azd.sh | bash
   ```

2. Verify Docker Desktop is running:

   ```powershell
   docker info
   ```

   If this command fails, start Docker Desktop and wait for it to fully initialize before continuing.

3. Log in to Azure:

   ```powershell
   az login
   azd auth login
   ```

---

## Step 2: Review the Agent Definition

1. Open `src/workshop_lab_agent_host/agent.yaml` and confirm:
   - `kind: hosted`
   - `protocol: responses` at version `v1`
   - Environment variables `AZURE_AI_PROJECT_ENDPOINT` and `MODEL_DEPLOYMENT_NAME` are listed

2. Open `src/workshop_lab_agent_host/Dockerfile` and confirm the build uses the Python 3.12 slim image and targets `linux/amd64`.

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

---

## Step 4: Provision Azure Resources

Preview then provision the required Azure resources:

```powershell
azd provision --preview
azd provision
```

> **Note:** You need Contributor access on your Azure subscription for resource provisioning. Provisioning takes approximately 2–3 minutes and creates the Azure Container Registry.

After provisioning, capture the output values:

```powershell
azd env get-values
```

Confirm the following values are present:

- `AZURE_CONTAINER_REGISTRY_NAME`
- `AZURE_CONTAINER_REGISTRY_ENDPOINT`
- `AZURE_AI_PROJECT_ENDPOINT`
- `MODEL_DEPLOYMENT_NAME`

---

## Step 5: Test the Agent Locally

Before publishing to Azure, confirm the agent works in your local environment.

1. Set the required environment variables for your session:

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

2. Run the hosted agent:

   ```powershell
   uv run python src/workshop_lab_agent_host/main.py
   ```

3. In a second terminal, send a test request:

   **Windows (PowerShell):**

   ```powershell
   Invoke-RestMethod -Method Post `
       -Uri "http://localhost:8088/responses" `
       -ContentType "application/json" `
       -Body '{"input":"Should we use a hosted agent for our team onboarding workflow?"}'
   ```

   **macOS / Linux:**

   ```bash
   curl -X POST http://localhost:8088/responses \
     -H "Content-Type: application/json" \
     -d '{"input":"Should we use a hosted agent for our team onboarding workflow?"}'
   ```

   You should receive a structured recommendation from the Readiness Coach.

   Alternatively, open `src/workshop_lab_agent_host/run-requests.http` and use the VS Code REST Client extension to send the pre-built requests.

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

> **Important:** The build context must be `.` (the repo root) because the Dockerfile copies `pyproject.toml`, `uv.lock`, and the `src/` packages. Use `--file` to point to the Dockerfile.

**Windows (PowerShell):**

```powershell
$acrName = (azd env get-values | Select-String "AZURE_CONTAINER_REGISTRY_NAME").ToString().Split("=")[1].Trim('"')
az acr build --registry $acrName --image workshoplab-agent:lab4 --platform linux/amd64 `
    --file ./src/workshop_lab_agent_host/Dockerfile `
    .
```

**macOS / Linux:**

```bash
ACR_NAME=$(azd env get-values | grep AZURE_CONTAINER_REGISTRY_NAME | cut -d'=' -f2 | tr -d '"')
az acr build --registry "$ACR_NAME" --image workshoplab-agent:lab4 --platform linux/amd64 \
    --file ./src/workshop_lab_agent_host/Dockerfile \
    .
```

> **Note:** The first build takes 2–4 minutes as the base Python images are downloaded. Subsequent builds are faster.

When the build completes, note the full image URI shown in the output:

```
<acr-name>.azurecr.io/workshoplab-agent:lab4
```

---

## Step 7: Deploy the Hosted Agent to Foundry

Deploy the hosted agent definition to your Foundry project using the direct deployment script. Replace `<acr-name>` with the name captured in Step 6:

**Windows (PowerShell):**

```powershell
./scripts/deploy-hosted-agent-direct.ps1 `
    -ImageUri "<acr-name>.azurecr.io/workshoplab-agent:lab4"
```

**macOS / Linux:**

```bash
python scripts/deploy_foundry_agent.py \
    --project-endpoint "$AZURE_AI_PROJECT_ENDPOINT" \
    --agent-name hosted-agent-readiness-coach \
    --manifest src/workshop_lab_agent_host/agent.yaml \
    --set "AZURE_AI_PROJECT_ENDPOINT=$AZURE_AI_PROJECT_ENDPOINT" \
    --set "chat=$MODEL_DEPLOYMENT_NAME"
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

**Windows (PowerShell):**

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

**macOS / Linux:**

```bash
ACCOUNT_NAME="<resource-account-name>"   # e.g. my-foundry-account
PROJECT_NAME="<foundry-project-name>"    # e.g. my-foundry-project

az cognitiveservices agent start \
    --account-name "$ACCOUNT_NAME" \
    --project-name "$PROJECT_NAME" \
    --name hosted-agent-readiness-coach \
    --agent-version 1 \
    --show-logs
```
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

1. Open `src/workshop_lab_agent_host/run-requests.http`.
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

**Windows (PowerShell):**

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

**macOS / Linux:**

```bash
PROJECT_ENDPOINT="https://<account>.services.ai.azure.com/api/projects/<project>"
TOKEN=$(az account get-access-token --resource "https://ai.azure.com" --query accessToken -o tsv)

curl -X POST "$PROJECT_ENDPOINT/openai/v1/responses" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H "api-version: 2025-01-01-preview" \
  -d '{
    "input": [{"role": "user", "content": "We are onboarding a team to Microsoft Foundry hosted agents. We need private API access, a repeatable deployment process, and a launch checklist for production readiness. Should we use a hosted agent for this scenario, and what implementation shape do you recommend?"}],
    "agent_reference": {"name": "hosted-agent-readiness-coach", "type": "agent_reference"}
  }'
```

Validation checks:
- the HTTP request succeeds without a 4xx or 5xx response
- the response object status is `completed`
- the answer includes both a hosted-agent recommendation and implementation guidance

You can also use the pre-built production requests in `src/workshop_lab_agent_host/run-requests.http`.

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
| Docker build errors | Ensure Docker Desktop is running: `docker info` |
| `SubscriptionNotRegistered` error | Register providers: `az provider register --namespace Microsoft.CognitiveServices` |
| `AuthorizationFailed` during provisioning | Request Contributor role on your subscription or resource group |
| Agent doesn't start locally | Verify environment variables are set and run `az login` to refresh credentials |
| `AcrPullUnauthorized` error | Grant AcrPull role to the project's managed identity on the container registry |
| azd version too old | Run `winget upgrade Microsoft.Azd` (Windows), `brew upgrade azd` (macOS), or re-run the install script (Linux) |
| Model not found | Verify the deployment name in Foundry → Build → Deployments matches `MODEL_DEPLOYMENT_NAME` |
| `azd provision` fails with existing resource group | Choose a unique environment name or delete the existing resource group first |

---

## What This Lab Demonstrates

This lab follows the [Microsoft Learn hosted-agent quickstart](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?pivots=azd) adapted for Python:

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