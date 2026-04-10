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

# Create agent definition JSON inline and call the Python deployment helper
$agentDefinition = @{
    kind = "hosted"
    image = $ImageUri
    cpu = "1"
    memory = "2Gi"
    container_protocol_versions = @(
        @{
            protocol = "responses"
            version = "v1"
        }
    )
    environment_variables = @{
        AZURE_AI_PROJECT_ENDPOINT = $ProjectEndpoint
        MODEL_DEPLOYMENT_NAME = $ModelDeploymentName
    }
} | ConvertTo-Json -Depth 10

Write-Host "Creating hosted agent '$AgentName' with image: $ImageUri"
Write-Host "Project: $ProjectEndpoint"
Write-Host "Deployment: $ModelDeploymentName"
Write-Host ""
Write-Host "Agent definition prepared (details suppressed in logs)."
Write-Host ""

# Call the Python deployment helper with the agent definition
python scripts/deploy_foundry_agent.py `
    --project-endpoint $ProjectEndpoint `
    --agent-name $AgentName `
    --agent-definition $agentDefinition `
    --set "AZURE_AI_PROJECT_ENDPOINT=$ProjectEndpoint" `
    --set "MODEL_DEPLOYMENT_NAME=$ModelDeploymentName"

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
