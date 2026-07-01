# Known Issues and Workarounds

This page collects the issues you are most likely to hit while working through the labs, with copy-paste workarounds. Lab 4 links to the numbered issues below.

> **Hosted agents are in preview.** APIs, regions, and CLI commands can change between updates. When a command behaves differently from the docs, check the current [Microsoft Learn hosted-agent quickstart](https://learn.microsoft.com/azure/foundry/agents/quickstarts/quickstart-hosted-agent?pivots=azd) for the latest syntax.

> **Note:** Issue 7 is fixed in the repo, and Issue 1 is hardened with `global.json`. Issues 2–6 are environmental (Azure region/SKU, RBAC, CLI version, or manual build context) rather than repo defects — they are documented workarounds for conditions outside the codebase.

---

## Top blockers (seen during a live Lab 4 deployment)

These two are the most common reasons a *successfully deployed* agent still fails when you invoke it. Both are confirmed from a real end-to-end run.

### A. `azd ai agent invoke` returns `401 PermissionDenied`

**Symptom:** The agent deploys and shows `active`, but invoking it returns `401 PermissionDenied` — either "lacks the required data action …/chat/completions/action" or "Principal does not have access to API/Operation."

**Cause:** The agent runs as its own managed identity. This workshop's code resolves the model connection and calls the model itself, so that identity needs data-plane roles that are not always granted automatically.

**Fix:** Grant the agent's managed identity two roles on the Foundry account, then let the container idle (scale to zero) so it refreshes its token:

```powershell
az role assignment create --assignee-object-id <agent-identity-object-id> --assignee-principal-type ServicePrincipal `
  --role "Cognitive Services OpenAI User" `
  --scope "/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<account>"
az role assignment create --assignee-object-id <agent-identity-object-id> --assignee-principal-type ServicePrincipal `
  --role "Azure AI Developer" `
  --scope "/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<account>"
```

The object id appears in the first 401 error message. See [Lab 0 — Required Azure permissions](labs/lab-0-foundry-setup/lab-0_readme.md#required-azure-permissions). After granting roles, wait a few minutes for RBAC propagation and stop invoking so the container cold-starts with a fresh token.

### B. Deployed container fails with `TypeLoadException: UserInputRequestContent`

**Symptom:** The container starts but every invoke fails with `Could not load type 'Microsoft.Extensions.AI.UserInputRequestContent' from assembly 'Microsoft.Extensions.AI.Abstractions, Version=10.7.0.0'`.

**Cause:** `Microsoft.Extensions.AI*` resolved to 10.4+ (which removed `UserInputRequestContent`), while `Azure.AI.AgentServer` beta.11 still references it.

**Fix:** Keep `Microsoft.Extensions.AI`, `Microsoft.Extensions.AI.Abstractions`, and `Microsoft.Extensions.AI.OpenAI` pinned to **10.3.0** in `src/WorkshopLab.AgentHost/WorkshopLab.AgentHost.csproj`. The repo's `.github/dependabot.yml` already ignores `>=10.4.0` for these so an automated bump can't reintroduce the break. Redeploy after fixing the pin.

> **Model note:** the workshop uses `gpt-5.4-mini` (a current GA model). `gpt-4.1-mini` is in the deprecating lifecycle — pick any GA chat model available in your region if you change it.

---

## Issue 1: `dotnet build` or `dotnet test` fails with SDK errors

**Symptom:** Build errors that mention an unsupported language version or a missing framework.

**Cause:** The .NET 10 SDK is not installed or not first on your `PATH`.

**Hardened:** The repo now includes a [global.json](global.json) pinning the SDK to `10.0.100` with `rollForward: latestMinor`. Any installed 10.x SDK is accepted; an older SDK (8.x/9.x) fails immediately with a clear message instead of confusing build errors.

**Workaround:**

```powershell
dotnet --version   # must report a 10.x SDK
```

Install the .NET 10 SDK from [dot.net/download](https://dot.net/download) if the version is lower than 10.

---

## Issue 2: `SkuNotSupported` (or a region error) during `azd provision`

**Symptom:** `azd provision` fails while creating the Azure Container Registry with a `SkuNotSupported` error, or the region rejects the resource.

**Cause:** The selected region does not offer the requested ACR SKU, or the subscription is restricted in that region.

**Workaround:**

- Choose a different region for the azd environment:

  ```powershell
  azd env set AZURE_LOCATION "eastus2"
  azd provision
  ```

- Or reuse an existing registry by pointing the deployment at it instead of creating a new one.
- Confirm the resource provider is registered:

  ```powershell
  az provider register --namespace Microsoft.ContainerRegistry
  ```

---

## Issue 3: ACR build fails with a missing project reference

**Symptom:** `az acr build` fails because it cannot find `WorkshopLab.Core` while building `WorkshopLab.AgentHost`.

**Cause:** The build context was set to `./src/WorkshopLab.AgentHost` instead of `./src`. The Dockerfile copies **both** `WorkshopLab.Core` and `WorkshopLab.AgentHost`, so it needs the parent `src` folder as context.

**Workaround:** Use `./src` as the build context and point `--file` at the Dockerfile:

```powershell
az acr build --registry $acrName --image workshoplab-agent:lab4 --platform linux/amd64 `
    --file ./src/WorkshopLab.AgentHost/Dockerfile `
    ./src
```

---

## Issue 4: `AcrPullUnauthorized` when the hosted agent starts

**Symptom:** The container fails to start and the logs show an image-pull authorization error.

**Cause:** The Foundry project's managed identity (the agent identity) does not have permission to pull from your registry.

**Workaround:** Grant the agent identity the **AcrPull** (or **Container Registry Repository Reader**) role on the registry, then start the agent again. See [Deploy a hosted agent with a private Azure Container Registry](https://learn.microsoft.com/azure/foundry/agents/how-to/deploy-hosted-agent-private-azure-container-registry).

---

## Issue 5: `linux/amd64` platform mismatch

**Symptom:** The container builds locally but fails to start in Foundry, or reports an incompatible architecture.

**Cause:** The image was built for ARM (for example, on Apple Silicon). The hosting platform requires x86_64 (`linux/amd64`) images.

**Workaround:** Always build with the platform flag:

```powershell
az acr build --platform linux/amd64 ...
# or, for a local build
docker build --platform linux/amd64 .
```

---

## Issue 6: Container start returns 404, or `az cognitiveservices agent` is not found

**Symptom:** Starting the hosted agent container returns a 404, or the Azure CLI reports that the `agent` subcommand does not exist.

**Cause:** The `az cognitiveservices agent` commands require a recent Azure CLI. Older versions do not include them.

**Workaround:**

- Update the Azure CLI to version 2.80 or later:

  ```powershell
  az upgrade
  az version
  ```

- Start the agent with the current command shape:

  ```powershell
  az cognitiveservices agent start `
      --account-name <resource-account-name> `
      --project-name <foundry-project-name> `
      --name hosted-agent-readiness-coach `
      --agent-version 1 `
      --show-logs
  ```

- Check status:

  ```powershell
  az cognitiveservices agent status `
      --account-name <resource-account-name> `
      --project-name <foundry-project-name> `
      --name hosted-agent-readiness-coach `
      --agent-version 1
  ```

You can also start and check the agent from the [Foundry portal](https://ai.azure.com/) under **Build → Agents**.

---

## Issue 7: `OpenTelemetry` moderate-severity restore warnings (NU1902) — resolved

**Status:** Resolved on 2026-07-01. The OpenTelemetry stack is pinned to `1.16.0` in `src/WorkshopLab.AgentHost/WorkshopLab.AgentHost.csproj`, which clears both advisories (GHSA-g94r-2vxg-569j and GHSA-4625-4j76-fww9). The solution builds with 0 warnings.

If you see `NU1902` again after a package bump, check whether a transitive dependency reintroduced an older `OpenTelemetry.*` version and raise the explicit pins to the latest coherent OpenTelemetry release.
