# Lab 4 — Deploy the Hosted Agent with `azd ai agent`

> **Progress:** Lab 4 of 5 — `Lab 0 → Lab 1 → Lab 2 → Lab 3 → [Lab 4] → Lab 5`

**Goal:** Deploy your hosted agent to Microsoft Foundry using the **`azd ai agent`** extension — the current recommended path. One tool handles the whole lifecycle: initialize, run locally, provision, deploy, and invoke.

**Time:** 30 minutes

**You will need:** Lab 3 completed.

**Reference:** [Microsoft Learn — Quickstart: Deploy your first hosted agent (azd)](https://learn.microsoft.com/azure/foundry/agents/quickstarts/quickstart-hosted-agent?pivots=azd)

## Two ways to deploy

| | Path | Best for | Where |
|---|---|---|---|
| ✅ **Recommended** | **`azd ai agent` extension** — one tool runs `init → run → provision → deploy → invoke` and automates ACR provisioning, image build/push, and agent registration | Everyone completing the workshop | **This lab (below)** |
| 🔧 Optional | **Manual ACR + SDK** — provision the registry, `az acr build` the image, register the version with the Foundry SDK, start it with the Azure CLI | Learning the internals, or bring-your-own-CI pipelines | [Lab 4 Appendix — Manual Hosted-Agent Deployment](lab-4-appendix-manual-deploy.md) |

This lab uses the **recommended** path. It replaces the older manual steps (provision an ACR by hand, `az acr build`, register with a custom SDK app, `az cognitiveservices agent start`) with a single guided flow. You do **not** need the appendix to finish the workshop — read it later only if you want to see what the tooling automates.

---

## About this repo's structure

The agent lives in [src/WorkshopLab.AgentHost](../../src/WorkshopLab.AgentHost) and references the shared [WorkshopLab.Core](../../src/WorkshopLab.Core) library. Because the code spans two projects, this lab deploys in **container mode** using the existing [Dockerfile](../../src/WorkshopLab.AgentHost/Dockerfile), whose build context is the `src` folder so both projects are included.

The container reads its Foundry connection from the environment. When Foundry runs your agent it injects `FOUNDRY_PROJECT_ENDPOINT` automatically, and `azd ai agent run` sets it for local runs — so you do **not** declare it yourself. The model deployment name comes from `AZURE_AI_MODEL_DEPLOYMENT_NAME`. The agent code already reads both.

---

## Step 1: Verify prerequisites

| Requirement | Verify with | Notes |
|---|---|---|
| azd version 1.23.0 or later | `azd version` | Update: `winget upgrade Microsoft.Azd` (Windows) / `brew upgrade azd` (macOS) |
| Azure CLI authenticated | `az login` | — |
| azd authenticated | `azd auth login` | — |
| Contributor access on your subscription | — | Required for `azd provision` |
| A deployed chat model (e.g. `gpt-5.4-mini`) | [ai.azure.com](https://ai.azure.com/) → **Build → Deployments** | Needed if you reuse an existing Foundry project |

```powershell
azd version
az login
azd auth login
```

---

## Step 2: Install the AI agent extension

The `azd ai agent` commands come from the `azure.ai.agents` extension:

```powershell
azd extension install azure.ai.agents
```

Verify the command surface is available:

```powershell
azd ai agent doctor
```

> **Checkpoint:** `azd ai agent doctor` runs a health check and reports any missing prerequisites with a suggested fix. Resolve anything it flags before continuing.

---

## Step 3: Initialize the agent from the existing code

Wire the existing agent project into the `azd ai agent` workflow (brownfield, container mode):

```powershell
azd ai agent init --from-code `
    --src ./src/WorkshopLab.AgentHost `
    --agent-name hosted-agent-readiness-coach `
    --deploy-mode container
```

This creates or updates `azure.yaml` with a service entry (`host: azure.ai.agent`) and keeps the existing [agent.yaml](../../src/WorkshopLab.AgentHost/agent.yaml).

> **Prefer interactive on your first run.** Drop `--from-code` and let `azd ai agent init` prompt you — it lists your subscription, region, and deployment mode. Choose **container** when asked for the deployment method.

> **Build context fix (if needed).** If a later `azd deploy` cannot find `WorkshopLab.Core` while building the image, set the Docker context to the `src` folder in `azure.yaml`:
>
> ```yaml
> services:
>   hosted-agent-readiness-coach:
>     project: ./src/WorkshopLab.AgentHost
>     host: azure.ai.agent
>     language: dotnet
>     docker:
>       path: ./Dockerfile
>       context: ../
> ```

---

## Step 4: Choose your Foundry project and model

**Option A — Use an existing Foundry project** (recommended for the workshop). Point `init` at it with its ARM resource ID, or set it now:

```powershell
azd env set AZURE_AI_PROJECT_ID "/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<account>/projects/<project>"
azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME "gpt-5.4-mini"
```

**Option B — Let azd create a new Foundry project** during `azd provision` (Step 6). Do not set `AZURE_AI_PROJECT_ID`; just pick the model:

```powershell
azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME "gpt-5.4-mini"
```

> **Tip:** You can find a project's ARM resource ID in the [Foundry portal](https://ai.azure.com/) under **Operate → Admin**, or with `az cognitiveservices account show`.

---

## Step 5: Run locally and smoke-test

Start the agent locally — `azd ai agent run` is the local server on port 8088:

```powershell
azd ai agent run --no-inspector
```

> **Wait for the ready line.** Watch the output for `Now listening on: http://localhost:8088` (not just a "Starting…" banner) before you invoke. Invoking too early returns `could not connect`.

In a second terminal, send a local invoke (no billing):

```powershell
azd ai agent invoke --local "We need private API access and a launch checklist. Should we use a hosted agent, and what shape do you recommend?"
```

Stop the local server (Ctrl+C in the run terminal) before continuing.

---

## Step 6: Provision Azure resources

```powershell
azd provision --preview
azd provision
```

This creates the container registry and — if you chose Option B — the Foundry project and model deployment.

> **Estimated Azure costs:** Provisioning creates a container registry (~**$0.17/day, ~$5/month**). The hosted agent container is billed per-second only while running. A short workshop session costs **under $1**. Run `azd down` when finished. See the [ACR pricing page](https://azure.microsoft.com/pricing/details/container-registry/).

> If `azd provision` fails with `SkuNotSupported` or a region error, see [Known Issues — Issue 2](../../knownissues.md).

---

## Step 7: Deploy the agent

```powershell
azd deploy
```

`azd deploy` builds the container image from the Dockerfile, pushes it to the registry, and registers a **new immutable agent version** in Foundry. The Foundry runtime pulls the image using the agent's managed identity, which `azd` grants **AcrPull** during deploy.

> **Automatic role grant.** A `postdeploy` hook ([scripts/grant-agent-identity-roles.ps1](../../scripts/grant-agent-identity-roles.ps1), wired in [azure.yaml](../../azure.yaml)) grants the agent's managed identity the three data-plane roles it needs to call the model — **Cognitive Services OpenAI User**, **Cognitive Services User**, and **Azure AI Developer**. If you lack permission to assign roles, the hook prints the manual commands and the deploy still succeeds. Allow a few minutes for RBAC to propagate before the first invoke.

> **Note:** The first build takes a few minutes while the .NET 10 base images download. If the build cannot find `WorkshopLab.Core`, apply the Docker context fix from Step 3.

---

## Step 8: Verify and invoke the deployed agent

Check the deployment status:

```powershell
azd ai agent show --output json
```

> **Checkpoint:** Expect `"status": "active"` (or `"deployed"`) and an `agent_endpoints` map.

Invoke the deployed agent (this calls the model and **incurs usage charges**):

```powershell
azd ai agent invoke "We are onboarding a team to Microsoft Foundry hosted agents. We need private API access, a repeatable deployment process, and a launch checklist for production readiness. Should we use a hosted agent, and what implementation shape do you recommend?"
```

Verify the reply includes a hosted-agent recommendation, an implementation shape, and operational guidance (checklist / prerequisites).

> **If the invoke returns `401 PermissionDenied`:** the agent's managed identity needs model access. The `postdeploy` hook grants this automatically; if it was skipped (you lacked role-assignment permission) grant all **three** roles on the Foundry account — **Cognitive Services OpenAI User**, **Cognitive Services User**, and **Azure AI Developer** (see [Lab 0 — Required Azure permissions](../lab-0-foundry-setup/lab-0_readme.md#required-azure-permissions) and [knownissues.md Blocker A](../../knownissues.md)). RBAC takes a few minutes to propagate, and the running container holds a token issued before the grant — so if it still 401s, re-run `azd deploy` to roll a fresh container that picks up the new access.

**Verify in the portal:** open the [Foundry portal](https://ai.azure.com/) → your project → **Build → Agents** → `hosted-agent-readiness-coach` → **Open in playground**, and send the same prompt.

**Verify with raw REST or the REST Client:** the deployed agent has a dedicated endpoint. Use the pre-built requests in [src/WorkshopLab.AgentHost/run-requests.http](../../src/WorkshopLab.AgentHost/run-requests.http) (the "Production requests" section), which POST to `agents/<name>/endpoint/protocols/openai/responses` with the `Foundry-Features` header.

---

## Step 9 (optional): Smoke-test the agent

A smoke test posts a few prompts to the deployed agent and checks the replies — a fast gate that catches broken deployments, missing configuration, or permission issues before you trust a release. It is not a replacement for full evaluations; it just confirms the agent is reachable, responding, and following its prompt.

Run it locally against your deployed agent:

```powershell
dotnet run --project src/WorkshopLab.SmokeTests -- `
    --project-endpoint "<your-project-endpoint>" `
    --agent-name hosted-agent-readiness-coach
```

Expected output ends with `=== Summary: 5/5 passed ===`.

### Run the smoke test in GitHub Actions

The [Smoke Test Hosted Agent](../../.github/workflows/smoke-test.yml) workflow runs the same checks in CI so every deployment can be validated automatically. Set it up once in your fork:

1. **Configure Azure sign-in (OIDC).** The simplest way is `azd pipeline config`, which creates a service principal, adds a GitHub federated credential (no stored secret), and sets the `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, and `AZURE_SUBSCRIPTION_ID` repository secrets the workflow expects:

   ```powershell
   azd pipeline config
   ```

   You can also create these manually under **Settings → Secrets and variables → Actions → Secrets** (an Entra app registration with a GitHub federated credential).

2. **Add the project endpoint** as a repository **variable** (**Settings → Secrets and variables → Actions → Variables**):

   | Variable | Value |
   |---|---|
   | `FOUNDRY_PROJECT_ENDPOINT` | `https://<account>.services.ai.azure.com/api/projects/<project>` |

3. **Give the CI identity permission to invoke the agent.** The workflow acquires a token as its service principal and calls the agent's Responses endpoint, so that identity needs data-plane access on the Foundry account:

   ```powershell
   az role assignment create `
     --assignee <AZURE_CLIENT_ID> `
     --role "Azure AI Developer" `
     --scope "/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<account>"
   ```

4. **Run it.** Open **Actions → Smoke Test Hosted Agent → Run workflow** (optionally override the agent name). A green run ends with `=== Summary: 5/5 passed ===`; a failure prints the failing assertion and the response text.

**Gate deployments automatically:** the workflow also exposes a `workflow_call` trigger, so a deploy pipeline can call it after `azd deploy` (passing `project_endpoint` and `agent_name`), or you can add the reusable [smoke-test action](../../.github/actions/smoke-test/action.yml) as a step in your own deploy job. Tune the prompts and assertions in [deployment/smoke-tests.json](../../deployment/smoke-tests.json) to match your agent.

> Based on [Smoke Test Microsoft Foundry Agents with GitHub Actions](https://techcommunity.microsoft.com/blog/azuredevcommunityblog/smoke-test-microsoft-foundry-agents-with-github-actions/4531912).

---

## Clean up resources

> ⛔ **Don't clean up yet.** [Lab 5](../lab-5-ui/lab-5_readme.md) connects a chat UI to this **deployed** agent. If you run `azd down` now, the hosted agent and everything `azd provision` created are deleted, and Lab 5 has nothing to call.
>
> Leave the deployment running until you finish Lab 5. The teardown commands live at the end of Lab 5, under [**Clean up resources**](../lab-5-ui/lab-5_readme.md#clean-up-resources).

---

## Troubleshooting

| Problem | Solution |
|---|---|
| `extension not installed` | `azd extension install azure.ai.agents` |
| `not_logged_in` / `login_expired` | Run `az login` and `azd auth login` |
| `azd version` too old | `winget upgrade Microsoft.Azd` (Windows) / `brew upgrade azd` (macOS) |
| Image build cannot find `WorkshopLab.Core` | Set `docker.context: ../` on the service in `azure.yaml` (Step 3) |
| `401 PermissionDenied` on invoke | Grant the agent's managed identity **Cognitive Services OpenAI User** + **Azure AI Developer** on the Foundry account, then let the container idle so it picks up a fresh token. See [Lab 0 — Required Azure permissions](../lab-0-foundry-setup/lab-0_readme.md#required-azure-permissions) |
| Model `404` / wrong deployment on invoke | Confirm `AZURE_AI_MODEL_DEPLOYMENT_NAME` matches a real deployment in **Build → Deployments** |
| `AcrPullUnauthorized` when the container starts | Grant the agent identity **AcrPull** on the registry. See [Known Issues — Issue 4](../../knownissues.md) |
| `SkuNotSupported` during provisioning | Try another region or reuse an existing registry. See [Known Issues — Issue 2](../../knownissues.md) |
| `SubscriptionNotRegistered` | `az provider register --namespace Microsoft.CognitiveServices` |
| Anything else | Run `azd ai agent doctor` and follow its suggestions |

> **Full list of documented issues and workarounds:** see [knownissues.md](../../knownissues.md).

---

**Navigation:** [◀ Lab 3 — CI](../lab-3-ci/lab-3_readme.md) · [📚 All labs](../README.md) · [Next: Lab 5 — UI ▶](../lab-5-ui/lab-5_readme.md)

> Optional deep-dive: [Lab 4 Appendix — Manual Hosted-Agent Deployment](lab-4-appendix-manual-deploy.md)

---

## What this lab demonstrates

- Installing and using the `azd ai agent` extension
- Wiring existing agent code into the azd lifecycle (`azd ai agent init --from-code`)
- Local inner-loop with `azd ai agent run` + `azd ai agent invoke --local`
- Provisioning and deploying with `azd provision` + `azd deploy`
- Registering an immutable agent version and invoking it through its dedicated endpoint

For the manual ACR + SDK equivalent (optional background), see [Lab 4 Appendix](lab-4-appendix-manual-deploy.md).

---

## Expected result

The `hosted-agent-readiness-coach` agent is deployed and `active` in Foundry Agent Service. It responds in the Foundry playground and through its dedicated `agents/<name>/endpoint/protocols/openai/responses` endpoint with implementation-shape recommendations, launch checklists, and troubleshooting guidance.
