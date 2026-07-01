# Known Issues

This file documents known issues and workarounds for the workshop labs.

---

## Issue 1 — `AuthenticationError` or `DefaultAzureCredential` failure

**Symptom:** The hosted agent fails to start locally with an `AuthenticationError` or `DefaultAzureCredential` exception.

**Fix:** Run `az login` again to refresh your Azure CLI session, then restart the agent.

---

## Issue 2 — `SkuNotSupported` during ACR provisioning

**Symptom:** `azd provision` fails with a `SkuNotSupported` error when creating the Azure Container Registry.

**Cause:** The `Standard` SKU for Azure Container Registry is not available in the selected region.

**Fix:** Try one of the following:

1. Re-run `azd provision` targeting a different region:

   ```powershell
   azd env set AZURE_LOCATION "eastus2"
   azd provision
   ```

2. Reuse an existing ACR in a supported region by setting the registry name manually and skipping ACR provisioning in the Bicep templates.

---

## Issue 3 — ACR build fails with missing project reference

**Symptom:** `az acr build` fails with an error such as `error MSB3202: The project file ... was not found`.

**Cause:** The build context was set to `./src/WorkshopLab.AgentHost` instead of `./src`. The Dockerfile uses a multi-stage build that copies both `WorkshopLab.Core` and `WorkshopLab.AgentHost`, so the entire `./src` directory must be available as the build context.

**Fix:** Use `./src` as the build context and `--file` to point to the Dockerfile:

```powershell
az acr build --registry $acrName --image workshoplab-agent:lab4 --platform linux/amd64 `
    --file ./src/WorkshopLab.AgentHost/Dockerfile `
    ./src
```

---

## Issue 4 — `SubscriptionNotRegistered` error

**Symptom:** `azd provision` or an Azure CLI command fails with `SubscriptionNotRegistered`.

**Fix:** Register the required resource providers:

```powershell
az provider register --namespace Microsoft.CognitiveServices
az provider register --namespace Microsoft.ContainerRegistry
```

Registrations can take a few minutes. Re-run `azd provision` once the registration state shows `Registered`.

---

## Issue 5 — `AcrPullUnauthorized` when the agent container starts

**Symptom:** The hosted agent container fails to start and the logs show `AcrPullUnauthorized` or a similar pull-permission error.

**Fix:** Grant the `AcrPull` role to the Foundry project's managed identity on the container registry:

```powershell
$acrId = (az acr show --name $acrName --query id -o tsv)
$identityId = (az cognitiveservices account show --name $accountName --resource-group $rgName --query identity.principalId -o tsv)
az role assignment create --assignee $identityId --role AcrPull --scope $acrId
```

---

## Issue 6 — Container start returns 404 or `az cognitiveservices agent start` is not found

**Symptom:** Calling `az cognitiveservices agent start` returns a `404` response or the command is not recognized.

**Cause:** The Azure CLI version is older than 2.80, which introduced the `cognitiveservices agent` command group.

**Fix:** Update the Azure CLI to version 2.80 or later:

```powershell
# Windows
winget upgrade Microsoft.AzureCLI

# macOS
brew upgrade azure-cli
```

After updating, log in again and retry:

```powershell
az login
az cognitiveservices agent start `
    --account-name $accountName `
    --project-name $projectName `
    --name hosted-agent-readiness-coach `
    --agent-version 1 `
    --show-logs
```
