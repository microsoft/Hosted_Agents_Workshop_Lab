# Workshop Lab for Microsoft Foundry Hosted Agents

This repository is a beginner-friendly workshop for building a Microsoft Foundry Hosted Agent with .NET 10.

If you are new to Foundry hosted agents, follow the labs in order from Lab 0 to Lab 5. Each lab builds on the previous lab and keeps commands copy-paste ready.

The scenario is a **Hosted Agent Readiness Coach**. The agent helps delivery teams answer practical questions such as:

- Should this use case start as a prompt agent or a hosted agent?
- What should a pilot launch checklist include?
- How should a team troubleshoot common early setup problems?

## What You Will Learn

By the end of the workshop, you will have worked through the full development path for a simple hosted agent:

1. Environment setup and local run
2. Copilot customization
3. Feature implementation with local tools
4. CI automation
5. Deployment preparation for hosted agents
6. UI integration for end-to-end usage

## How This Repository Is Structured

This repository demonstrates three distinct stages:

- local and CI validation inside the repo
- Azure resource provisioning and image publishing for deployment readiness
- Microsoft Foundry control-plane deployment as an explicit separate step

That split is intentional. `azd` provisions the Azure resource group and Azure Container Registry, GitHub Actions can publish the agent image to ACR, and the final Foundry agent create or update step remains explicit because it depends on your real project endpoint, manifest values, and hosted-agent lifecycle controls.

## What You Build

You build a code-based hosted agent that exposes the OpenAI Responses-compatible `/responses` endpoint on port `8088`.

The agent uses deterministic local tools backed by `WorkshopLab.Core`:

- `RecommendImplementationShape`
- `BuildLaunchChecklist`
- `TroubleshootHostedAgent`

These tools make the scenario useful for teams who are evaluating or onboarding to Microsoft Foundry Hosted Agents.

## Before You Start

Before working through the workshop, make sure you have the accounts, access, and tools the labs assume.

- An Azure subscription. A trial subscription is fine if it can create and use Microsoft Foundry resources, or you can bring your own subscription.
- Permission to sign in with Azure CLI and Azure Developer CLI using the account that will run the workshop.
- Sufficient Azure access to the target subscription and resource group. At minimum, you should be able to provision workshop resources, use the Foundry project endpoint, and deploy or use a chat model.
- A GitHub account, since the later labs use repository workflows and GitHub Actions.
- Permission to create or update GitHub Actions workflows and repository settings in the repo used for the workshop.

If you are using a shared enterprise environment, make sure you know in advance which subscription, resource group, Microsoft Foundry project, and model deployment you are expected to use.

## Getting Started

If you are new to this topic, use the repo in this order:

1. Read the setup requirements in this README.
2. Follow the lab sequence in the course map below.
3. Run the agent locally before thinking about Azure deployment.
4. Use the Azure provisioning and deployment sections only after the local and CI steps are working.

## Beginner Path (Recommended)

If this is your first time with hosted agents, use this simple path:

1. Complete Lab 0 and confirm `/responses` works locally.
2. Complete Labs 1 to 3 and make sure tests and CI pass.
3. Complete Lab 4 to deploy and verify your hosted agent in Foundry.
4. Complete Lab 5 to use the deployed agent through a UI.

## Course Map: Quick Navigation by Chapter

Use the labs in order. Each one builds on the previous one.

| Lab | Focus | What You Finish With |
| --- | --- | --- |
| [Lab 0](labs/lab-0-foundry-setup/lab-0_readme.md) | Foundry setup | A working local hosted agent and a validated `/responses` endpoint |
| [Lab 1](labs/lab-1-copilot-config/lab-1_readme.md) | Copilot config | Repo-specific Copilot guidance for this hosted-agent project |
| [Lab 2](labs/lab-2-implementation-shape/lab-2_readme.md) | Implementation shape | A real feature change in one of the agent's deterministic tools |
| [Lab 3](labs/lab-3-ci/lab-3_readme.md) | CI | Build, test, and container validation in GitHub Actions |
| [Lab 4](labs/lab-4-deploy/lab-4_readme.md) | Deploy | Azure-ready packaging plus the deployment handoff steps |
| [Lab 5](labs/lab-5-ui/lab-5_readme.md) | UI | A working chat UI that calls your deployed hosted agent |

For the full lab guide, see [labs/README.md](labs/README.md).

## Repository Structure

```text
.
├── .devcontainer/              # Codespaces/dev container configuration
├── .github/workflows/         # CI and deployment-oriented workflows
├── labs/                      # Guided labs in sequential order
├── src/
│   ├── WorkshopLab.AgentHost/ # Hosted agent entrypoint, Dockerfile, agent.yaml
│   └── WorkshopLab.Core/      # Deterministic domain logic used by the agent tools
├── tests/WorkshopLab.Tests/   # xUnit tests for the deterministic core logic
└── WorkshopLab.sln
```

## Quick Start

Use this section if you want to prove the app works locally before starting the later Azure steps.

### Required Tools

- .NET 10 SDK
- Azure CLI
- Azure Developer CLI (`azd`)
- Access to a Microsoft Foundry project
- A deployed chat model in that project

### Environment Variables

The agent host expects:

- `AZURE_AI_PROJECT_ENDPOINT`
- `MODEL_DEPLOYMENT_NAME`

Example PowerShell session:

```powershell
$env:AZURE_AI_PROJECT_ENDPOINT = "https://<resource>.services.ai.azure.com/api/projects/<project>"
$env:MODEL_DEPLOYMENT_NAME = "gpt-4.1-mini"
```

### Run Locally

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project src/WorkshopLab.AgentHost
```

Then send a local request:

```powershell
$body = @{
    input = "We need an internal agent that uses private APIs and workflow handoffs. Should we start with a hosted agent?"
    stream = $false
} | ConvertTo-Json

Invoke-RestMethod -Uri http://localhost:8088/responses -Method Post -Body $body -ContentType "application/json"
```

## Azure Provisioning With azd

Use this section when you are ready to provision Azure resources for the deployment path.

This repo includes an `azd` and `infra/` path for the Azure-owned part of deployment.

1. Create an environment:

```powershell
azd env new <environment-name>
```

2. Preview the infrastructure:

```powershell
azd provision --preview
```

3. Provision the resource group and Azure Container Registry:

```powershell
azd provision
```

The Bicep template in [infra/main.bicep](infra/main.bicep) provisions an Azure Container Registry and exports:

- `AZURE_CONTAINER_REGISTRY_NAME`
- `AZURE_CONTAINER_REGISTRY_ENDPOINT`
- `AZURE_RESOURCE_GROUP_NAME`

You still need to set your Foundry-specific environment values in the azd environment or shell:

- `AZURE_AI_PROJECT_ENDPOINT`
- `MODEL_DEPLOYMENT_NAME`

Example:

```powershell
azd env set AZURE_AI_PROJECT_ENDPOINT "https://<resource>.services.ai.azure.com/api/projects/<project>"
azd env set MODEL_DEPLOYMENT_NAME "gpt-4.1-mini"
```

## Publish To ACR

Use this step after `azd provision` when you are ready to publish the hosted-agent image.

The workflow in [.github/workflows/deploy.yml](.github/workflows/deploy.yml) publishes the hosted-agent image to Azure Container Registry and stops there.

It uses Azure OIDC login and the recommended cloud-build path:

- `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, and `AZURE_SUBSCRIPTION_ID` as GitHub secrets
- `AZURE_CONTAINER_REGISTRY_NAME` as a GitHub repository variable, usually copied from `azd env get-values`

The workflow does not create or start the Foundry hosted agent. That remains a separate step.

## Apply The Foundry Manifest

Use this step after the image is available in ACR.

Use the helper project in [src/WorkshopLab.FoundryDeployment/Program.cs](src/WorkshopLab.FoundryDeployment/Program.cs) or the wrapper script in [scripts/deploy-foundry-agent.ps1](scripts/deploy-foundry-agent.ps1) to call the Foundry manifest APIs explicitly.

Example:

```powershell
./scripts/deploy-foundry-agent.ps1
```

The wrapper reads:

- `AZURE_AI_PROJECT_ENDPOINT`
- `MODEL_DEPLOYMENT_NAME`
- optional `FOUNDRY_AGENT_ID`

If `FOUNDRY_AGENT_ID` is set, the helper updates that agent from manifest. Otherwise, it creates a new agent from manifest.

The helper intentionally stops after manifest create or update. Until the hosted-agent start and status control-plane surface is pinned cleanly in the SDK path, use the Foundry portal or the Foundry MCP tools for:

1. container start
2. status polling
3. final `/responses` verification

## How Deployment Works In This Workshop

This workshop separates deployment into a few clear stages so beginners can understand what happens where.

- Azure prerequisites are explicit: Azure CLI auth, a Foundry project endpoint, and a deployed chat model.
- The hosted agent contract is explicit in [src/WorkshopLab.AgentHost/agent.yaml](src/WorkshopLab.AgentHost/agent.yaml): `kind: hosted`, `responses` protocol, and environment-variable placeholders.
- Azure provisioning is explicit in [azure.yaml](azure.yaml) and [infra/main.bicep](infra/main.bicep): provision the Azure resource group path and Azure Container Registry with azd.
- CI publishing is explicit in [.github/workflows/deploy.yml](.github/workflows/deploy.yml): build a timestamped Linux AMD64 image and publish it to ACR with `az acr build`.
- Foundry manifest deployment is explicit in [src/WorkshopLab.FoundryDeployment/Program.cs](src/WorkshopLab.FoundryDeployment/Program.cs): create or update the hosted agent from manifest as a separate control-plane step.
- The remaining hosted-agent lifecycle operations are environment-dependent: start the container, verify status, and test the deployed agent.

That matches the Microsoft Foundry deploy skill workflow for hosted agents:

1. Detect the project and collect environment variables.
2. Build a Linux AMD64 container image.
3. Push the image to Azure Container Registry.
4. Create or update the hosted agent definition in Foundry.
5. Start the hosted agent container and verify it reaches `Running`.

The repo intentionally stops after ACR publish plus manifest apply so the remaining hosted-agent lifecycle actions stay tied to your real subscription, registry, and Foundry project.

## Notes for Hosted Agent Beginners

- Validate the `/responses` contract locally before attempting deployment.
- Keep the hosted agent HTTP server as the default process entrypoint.
- Build Linux AMD64 containers for hosted-agent deployment targets.
- Start with deterministic local tools before adding external connections.

## Security Hardening Note

For workshop safety and production readiness, apply these basics from day one:

- Keep credentials out of source control. Use environment variables, GitHub secrets, or Azure-managed identities.
- Treat `.env` files as local-only and commit only safe examples like `.env.example`.
- Keep dependencies and GitHub Actions updated regularly, and prefer pinned action SHAs in workflows.
- Run the pull-request secret scan workflow and resolve findings before merging.

## Project Metadata

- Contributor and coding-agent guidance: [AGENTS.md](AGENTS.md)
- Release and update history: [CHANGELOG.md](CHANGELOG.md)
