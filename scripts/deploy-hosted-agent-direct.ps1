# OPTIONAL / BACKGROUND ONLY — manual hosted-agent deployment path.
# The recommended path is the `azd ai agent` extension (see labs/lab-4-deploy/lab-4_readme.md).
# This script belongs to the manual appendix (labs/lab-4-deploy/lab-4-appendix-manual-deploy.md):
# it registers a hosted-agent version from a prebuilt ACR image via WorkshopLab.FoundryDeployment.
param(
    [string]$ProjectEndpoint = $env:AZURE_AI_PROJECT_ENDPOINT,
    [string]$AgentName = "hosted-agent-readiness-coach",
    [Parameter(Mandatory = $true)]
    [string]$ImageUri,
    [string]$ModelDeploymentName = $env:MODEL_DEPLOYMENT_NAME
)

if ([string]::IsNullOrWhiteSpace($ProjectEndpoint)) {
    throw "AZURE_AI_PROJECT_ENDPOINT must be set before deploying the hosted agent."
}

if ([string]::IsNullOrWhiteSpace($ModelDeploymentName)) {
    throw "MODEL_DEPLOYMENT_NAME must be set before deploying the hosted agent."
}

Write-Host "Creating hosted agent '$AgentName' with image: $ImageUri"
Write-Host "Project: $ProjectEndpoint"
Write-Host "Deployment: $ModelDeploymentName"
Write-Host ""

# Register the hosted-agent version via the Azure.AI.Projects.Agents SDK
# (WorkshopLab.FoundryDeployment builds the typed HostedAgentDefinition).
$args = @(
    "run",
    "--project", "src/WorkshopLab.FoundryDeployment/WorkshopLab.FoundryDeployment.csproj",
    "--",
    "--project-endpoint", $ProjectEndpoint,
    "--agent-name", $AgentName,
    "--image", $ImageUri,
    "--cpu", "1",
    "--memory", "2Gi",
    "--protocol", "responses",
    "--protocol-version", "1.0.0",
    "--env", "AZURE_AI_PROJECT_ENDPOINT=$ProjectEndpoint",
    "--env", "MODEL_DEPLOYMENT_NAME=$ModelDeploymentName"
)

dotnet @args

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "Agent deployment started. Next steps:"
    Write-Host "1. Start the hosted agent container in Foundry control plane"
    Write-Host "2. Wait for container to reach Running state"
    Write-Host "3. Send a test request to verify the agent responds"
    Write-Host ""
    Write-Host "Use Foundry MCP tools to start the container:"
    Write-Host "   Agent Name: $AgentName"
    Write-Host "   Project: $ProjectEndpoint"
}
