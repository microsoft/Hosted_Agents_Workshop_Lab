<#
.SYNOPSIS
    Grants the deployed hosted agent's managed identity the data-plane roles it needs
    to call the model, so `azd ai agent invoke` (and the Lab 5 UI) work without a manual
    RBAC step.

.DESCRIPTION
    Runs automatically as an azd `postdeploy` hook (see azure.yaml). The hosted agent
    runs under its own managed identity, and this workshop's agent code resolves the
    model connection and calls the model itself — so that identity needs three roles on
    the Foundry account:
      - Cognitive Services OpenAI User   (chat/completions data action)
      - Cognitive Services User          (broader inference API operations)
      - Azure AI Developer               (agent/project data-plane access)

    The script is idempotent and non-fatal: if you lack permission to assign roles
    (needs Owner or User Access Administrator on the account), it prints the manual
    commands and exits 0 so the deployment still succeeds. See knownissues.md Blocker A.
#>

$ErrorActionPreference = 'Stop'

$agentName = if ($env:AZD_AGENT_NAME) { $env:AZD_AGENT_NAME } else { 'hosted-agent-readiness-coach' }

# Resolve the Foundry account resource id from azd env outputs.
$accountId = $env:AZURE_AI_ACCOUNT_ID
if ([string]::IsNullOrWhiteSpace($accountId)) {
    if ($env:AZURE_SUBSCRIPTION_ID -and $env:AZURE_RESOURCE_GROUP -and $env:AZURE_AI_ACCOUNT_NAME) {
        $accountId = "/subscriptions/$($env:AZURE_SUBSCRIPTION_ID)/resourceGroups/$($env:AZURE_RESOURCE_GROUP)/providers/Microsoft.CognitiveServices/accounts/$($env:AZURE_AI_ACCOUNT_NAME)"
    }
}

if ([string]::IsNullOrWhiteSpace($accountId)) {
    Write-Warning "Could not resolve the Foundry account id from azd env (AZURE_AI_ACCOUNT_ID). Skipping automatic role grant."
    exit 0
}

# Get the agent's managed identity (instance identity) principal id.
$principalId = $null
try {
    $agentJson = azd ai agent show $agentName --output json 2>$null
    if ($LASTEXITCODE -eq 0 -and $agentJson) {
        $principalId = ($agentJson | ConvertFrom-Json).instance_identity.principal_id
    }
}
catch {
    $principalId = $null
}

if ([string]::IsNullOrWhiteSpace($principalId)) {
    Write-Warning "Could not resolve the agent's managed identity principal id (azd ai agent show). Skipping automatic role grant."
    Write-Host  "After deploy, grant the roles manually — see knownissues.md Blocker A."
    exit 0
}

$roles = @(
    'Cognitive Services OpenAI User',
    'Cognitive Services User',
    'Azure AI Developer'
)

$granted = $true
foreach ($role in $roles) {
    Write-Host "Granting '$role' to agent identity $principalId ..."
    az role assignment create `
        --assignee-object-id $principalId `
        --assignee-principal-type ServicePrincipal `
        --role $role `
        --scope $accountId `
        --only-show-errors 1>$null 2>$null
    if ($LASTEXITCODE -ne 0) {
        # Treat an existing assignment as success; anything else is a real failure.
        $existing = az role assignment list --assignee $principalId --scope $accountId --query "[?roleDefinitionName=='$role'] | length(@)" -o tsv 2>$null
        if ($existing -ne '1') {
            $granted = $false
            Write-Warning "Failed to grant '$role' (you may lack Owner / User Access Administrator on the account)."
        }
    }
}

if (-not $granted) {
    Write-Host ""
    Write-Warning "One or more roles were not assigned automatically. Grant them manually:"
    Write-Host "  `$mi   = '$principalId'"
    Write-Host "  `$acct = '$accountId'"
    Write-Host "  foreach (`$r in @('Cognitive Services OpenAI User','Cognitive Services User','Azure AI Developer')) {"
    Write-Host "    az role assignment create --assignee-object-id `$mi --assignee-principal-type ServicePrincipal --role `$r --scope `$acct"
    Write-Host "  }"
    Write-Host "See knownissues.md Blocker A. The deployment itself succeeded."
    exit 0
}

Write-Host ""
Write-Host "Agent identity roles granted. Allow a few minutes for RBAC to propagate to the data plane."
Write-Host "If the first 'azd ai agent invoke' still returns 401, re-run 'azd deploy' to roll a fresh container that picks up a token issued after the grant."
