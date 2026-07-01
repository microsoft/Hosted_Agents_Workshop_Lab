# OPTIONAL / BACKGROUND ONLY — manual manifest-based hosted-agent deployment path.
# The recommended path is the `azd ai agent` extension (see labs/lab-4-deploy/lab-4_readme.md).
# This script belongs to the manual appendix (labs/lab-4-deploy/lab-4-appendix-manual-deploy.md):
# it registers a hosted-agent version from the declarative agent.yaml manifest.
param(
    [string]$ProjectEndpoint = $env:AZURE_AI_PROJECT_ENDPOINT,
    [string]$Manifest = "src/WorkshopLab.AgentHost/agent.yaml",
    [string]$AgentName = "hosted-agent-readiness-coach",
    [string]$ImageUri = $env:AGENT_IMAGE,
    [string]$ModelDeploymentName = $env:MODEL_DEPLOYMENT_NAME
)

if ([string]::IsNullOrWhiteSpace($ProjectEndpoint)) {
    throw "AZURE_AI_PROJECT_ENDPOINT must be set before applying the Foundry manifest."
}

if ([string]::IsNullOrWhiteSpace($ModelDeploymentName)) {
    throw "MODEL_DEPLOYMENT_NAME must be set before applying the Foundry manifest."
}

if ([string]::IsNullOrWhiteSpace($ImageUri)) {
    throw "ImageUri (or AGENT_IMAGE) must be set so the manifest can reference the published container image."
}

# Registers the hosted-agent version from the declarative agent.yaml manifest.
# --set values are substituted into the manifest's ${NAME} / {{NAME}} placeholders,
# then WorkshopLab.FoundryDeployment builds the typed definition via the SDK.
$args = @(
    "run",
    "--project", "src/WorkshopLab.FoundryDeployment/WorkshopLab.FoundryDeployment.csproj",
    "--",
    "--project-endpoint", $ProjectEndpoint,
    "--agent-name", $AgentName,
    "--manifest", $Manifest,
    "--set", "AZURE_AI_PROJECT_ENDPOINT=$ProjectEndpoint",
    "--set", "chat=$ModelDeploymentName",
    "--set", "AGENT_IMAGE=$ImageUri"
)

dotnet @args