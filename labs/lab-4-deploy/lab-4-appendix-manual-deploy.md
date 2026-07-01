# Lab 4 Appendix — Manual Hosted-Agent Deployment (ACR + SDK)

> ⚠️ **Optional background — not required to complete the workshop.**
>
> The main [Lab 4](lab-4_readme.md) uses the **`azd ai agent`** extension, which is the current recommended path and does everything below for you in a few commands.
>
> This appendix keeps the **manual, "under-the-hood"** flow so you can see each moving part: provision an Azure Container Registry with Bicep, build and push the image with `az acr build`, register the hosted-agent version through the Foundry SDK, and start the container with the Azure CLI. Read it if you want to understand what the tooling automates, or if you are in a bring-your-own-project scenario where you publish images from your own CI pipeline. **You do not need to run it to finish the labs.**

**Reference:** [Microsoft Learn — Deploy a hosted agent with a private Azure Container Registry](https://learn.microsoft.com/azure/foundry/agents/how-to/deploy-hosted-agent-private-azure-container-registry)

---

## When you might use this path

- Your container image is built, scanned, signed, and pushed by a separate CI pipeline, and Foundry only consumes the finished image.
- You already have a Foundry project and just want to register and start a prebuilt image.
- You want to teach or learn the individual ACR / control-plane steps that `azd ai agent` hides.

The tooling used here:

- [infra/main.bicep](../../infra/main.bicep) — provisions the Azure Container Registry.
- [scripts/deploy-hosted-agent-direct.ps1](../../scripts/deploy-hosted-agent-direct.ps1) — registers the agent version from an inline definition (image, resources, protocol, env vars).
- [scripts/deploy-foundry-agent.ps1](../../scripts/deploy-foundry-agent.ps1) — alternative that registers from the declarative `agent.yaml` manifest.
- [src/WorkshopLab.FoundryDeployment](../../src/WorkshopLab.FoundryDeployment) - the console app both scripts call. It registers the hosted-agent version with the typed `Azure.AI.Projects.Agents` SDK (`AgentAdministrationClient.CreateAgentVersion`).

---

## Prerequisites

| Requirement | Verify with | Notes |
|---|---|---|
| azd version 1.23.0 or later | `azd version` | Update: `winget upgrade Microsoft.Azd` |
| Azure CLI 2.80 or later, authenticated | `az version`, `az login` | `az cognitiveservices agent` needs 2.80+ |
| Contributor access on your Azure subscription | — | Required for `azd provision` |
| A Foundry project + deployed chat model | [ai.azure.com](https://ai.azure.com/) | You bring these; this path does not create them |
| Docker Desktop _(optional)_ | `docker info` | Only for local builds; `az acr build` is cloud build |

---

## Step 1: Set the environment values

Create an azd environment (its name becomes the resource group `rg-<environment-name>`) and set your Foundry values:

```powershell
azd env new <environment-name>
azd env set AZURE_AI_PROJECT_ENDPOINT "https://<resource>.services.ai.azure.com/api/projects/<project>"
azd env set MODEL_DEPLOYMENT_NAME "gpt-5.4-mini"
```

**macOS / Linux** shell variables for local testing:

```bash
export AZURE_AI_PROJECT_ENDPOINT="https://<resource>.services.ai.azure.com/api/projects/<project>"
export MODEL_DEPLOYMENT_NAME="gpt-5.4-mini"
```

---

## Step 2: Provision the Azure Container Registry

```powershell
azd provision --preview
azd provision
```

> **Estimated Azure costs:** This provisions an Azure Container Registry (Standard SKU) — roughly **$0.17/day (~$5/month)**. The hosted agent container is billed per-second only while started. Expect **under $1** for a short session. Run `azd down` afterwards to stop charges. See the [ACR pricing page](https://azure.microsoft.com/pricing/details/container-registry/).

Confirm the outputs:

```powershell
azd env get-values
```

You should see `AZURE_CONTAINER_REGISTRY_NAME`, `AZURE_CONTAINER_REGISTRY_ENDPOINT`, `AZURE_AI_PROJECT_ENDPOINT`, and `MODEL_DEPLOYMENT_NAME`.

> If `azd provision` fails with `SkuNotSupported` or a region error, see [Known Issues — Issue 2](../../knownissues.md).

---

## Step 3: Test the agent locally

```powershell
$env:AZURE_AI_PROJECT_ENDPOINT = "https://<resource>.services.ai.azure.com/api/projects/<project>"
$env:MODEL_DEPLOYMENT_NAME = "gpt-5.4-mini"
dotnet run --project src/WorkshopLab.AgentHost
```

In a second terminal:

```powershell
Invoke-RestMethod -Method Post `
    -Uri "http://localhost:8088/responses" `
    -ContentType "application/json" `
    -Body '{"input":"Should we use a hosted agent for our team onboarding workflow?"}'
```

Stop the server with **Ctrl+C** when done.

---

## Step 4: Build and publish the container image

The build context must be `./src` (not `./src/WorkshopLab.AgentHost`) because the Dockerfile copies both `WorkshopLab.Core` and `WorkshopLab.AgentHost`.

```powershell
$acrName = (azd env get-values | Select-String "AZURE_CONTAINER_REGISTRY_NAME").ToString().Split("=")[1].Trim('"')
az acr build --registry $acrName --image workshoplab-agent:lab4 --platform linux/amd64 `
    --file ./src/WorkshopLab.AgentHost/Dockerfile `
    ./src
```

The image URI is `<acr-name>.azurecr.io/workshoplab-agent:lab4`.

> If the build fails with a missing project reference, confirm the context is `./src`. See [Known Issues — Issue 3](../../knownissues.md).

---

## Step 5: Register the hosted agent version in Foundry

```powershell
./scripts/deploy-hosted-agent-direct.ps1 `
    -ImageUri "<acr-name>.azurecr.io/workshoplab-agent:lab4"
```

The script registers the agent definition with the container image (`container_configuration.image`), CPU/memory, the `responses` protocol at version `1.0.0`, and the environment variables.

> **Manifest alternative:** `./scripts/deploy-foundry-agent.ps1 -ImageUri "<acr-name>.azurecr.io/workshoplab-agent:lab4"` registers from the declarative `agent.yaml` manifest instead. The manifest references the image through its `image: ${AGENT_IMAGE}` field, filled in from `-ImageUri`.

---

## Step 6: Start the hosted agent container

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

Check status:

```powershell
az cognitiveservices agent status `
    --account-name $accountName `
    --project-name $projectName `
    --name hosted-agent-readiness-coach `
    --agent-version 1
```

Expect `provisioningState: Running` and `health_state: Healthy`. If the container returns 404 or the command is missing, see [Known Issues — Issue 6](../../knownissues.md).

You can also start the agent from the [Foundry portal](https://ai.azure.com/) under **Build → Agents**.

---

## Step 7: Verify the deployed agent

Invoke the agent through its dedicated endpoint (see the same commands in the main [Lab 4](lab-4_readme.md) verification step):

```powershell
$projectEndpoint = "https://<account>.services.ai.azure.com/api/projects/<project>"
$agentName = "hosted-agent-readiness-coach"
$apiVersion = "2025-11-15-preview"
$tok = (az account get-access-token --resource "https://ai.azure.com" --query accessToken -o tsv)

$payload = @{ input = "Should we use a hosted agent for our onboarding workflow, and what implementation shape do you recommend?"; stream = $false } | ConvertTo-Json -Depth 5

Invoke-RestMethod -Method Post `
    -Uri "$projectEndpoint/agents/$agentName/endpoint/protocols/openai/responses?api-version=$apiVersion" `
    -Headers @{ "Authorization" = "Bearer $tok"; "Content-Type" = "application/json"; "Foundry-Features" = "HostedAgents=V1Preview" } `
    -Body $payload
```

---

## Clean up

> **Warning:** This permanently deletes the ACR and resources created by `azd provision`.

```powershell
azd down --preview
azd down
```

The Foundry project and hosted-agent definition remain unless deleted separately in the Foundry portal.

---

## What this appendix demonstrates

- Azure provisioning through `azd` + Bicep (ACR only)
- Cloud image publishing to ACR via `az acr build`
- Hosted-agent registration through the Foundry SDK (`WorkshopLab.FoundryDeployment`)
- Container start and running-state verification with the Azure CLI

The main [Lab 4](lab-4_readme.md) collapses all of this into the `azd ai agent` lifecycle. Prefer that path for real work — this appendix is here for background understanding only.

---

**Navigation:** [◀ Back to Lab 4 — Deploy](lab-4_readme.md) · [📚 All labs](../README.md) · [Next: Lab 5 — UI ▶](../lab-5-ui/lab-5_readme.md)
