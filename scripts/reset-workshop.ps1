<#
.SYNOPSIS
    Resets the workshop environment to a clean state for re-running labs.

.DESCRIPTION
    Tears down Azure resources provisioned by azd, clears environment variables,
    and removes local build artifacts. Useful for instructors resetting between
    sessions or learners starting over.

    This script asks for confirmation before destroying Azure resources.

.EXAMPLE
    ./scripts/reset-workshop.ps1
#>

param(
    [switch]$SkipAzure,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "=== Workshop Clean-State Reset ===" -ForegroundColor Cyan
Write-Host ""

# --- Step 1: Tear down Azure resources ---
if (-not $SkipAzure) {
    $hasAzd = Get-Command azd -ErrorAction SilentlyContinue
    if ($hasAzd) {
        Write-Host "Checking for azd environment..." -ForegroundColor Yellow
        $envOutput = azd env list 2>&1
        if ($LASTEXITCODE -eq 0 -and $envOutput -match '\S') {
            Write-Host "Active azd environments found."

            if (-not $Force) {
                Write-Host ""
                Write-Host "WARNING: This will permanently delete all Azure resources created by 'azd provision'." -ForegroundColor Red
                $confirm = Read-Host "Type 'yes' to continue, or anything else to skip Azure teardown"
                if ($confirm -ne "yes") {
                    Write-Host "Skipping Azure teardown." -ForegroundColor Yellow
                    $SkipAzure = $true
                }
            }

            if (-not $SkipAzure) {
                Write-Host "Running azd down..." -ForegroundColor Yellow
                azd down --force --purge
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "Azure resources removed." -ForegroundColor Green
                } else {
                    Write-Host "azd down returned a non-zero exit code. Check output above." -ForegroundColor Red
                }
            }
        } else {
            Write-Host "No azd environment found. Skipping Azure teardown." -ForegroundColor Gray
        }
    } else {
        Write-Host "azd not installed. Skipping Azure teardown." -ForegroundColor Gray
    }
} else {
    Write-Host "Azure teardown skipped (--SkipAzure flag)." -ForegroundColor Gray
}

# --- Step 2: Clear environment variables ---
Write-Host ""
Write-Host "Clearing workshop environment variables..." -ForegroundColor Yellow

$envVars = @(
    "AZURE_AI_PROJECT_ENDPOINT",
    "MODEL_DEPLOYMENT_NAME",
    "AZURE_CONTAINER_REGISTRY_NAME",
    "AZURE_CONTAINER_REGISTRY_ENDPOINT",
    "FOUNDRY_AGENT_ID"
)

foreach ($var in $envVars) {
    if (Test-Path "Env:\$var") {
        Remove-Item "Env:\$var"
        Write-Host "  Cleared $var" -ForegroundColor Gray
    }
}

Write-Host "Environment variables cleared." -ForegroundColor Green

# --- Step 3: Remove local build artifacts ---
Write-Host ""
Write-Host "Removing local build artifacts..." -ForegroundColor Yellow

$artifactDirs = @(
    "src/WorkshopLab.AgentHost/bin",
    "src/WorkshopLab.AgentHost/obj",
    "src/WorkshopLab.Core/bin",
    "src/WorkshopLab.Core/obj",
    "src/WorkshopLab.ChatUI/bin",
    "src/WorkshopLab.ChatUI/obj",
    "src/WorkshopLab.FoundryDeployment/bin",
    "src/WorkshopLab.FoundryDeployment/obj",
    "tests/WorkshopLab.Tests/bin",
    "tests/WorkshopLab.Tests/obj"
)

foreach ($dir in $artifactDirs) {
    $fullPath = Join-Path $PSScriptRoot ".." $dir
    if (Test-Path $fullPath) {
        Remove-Item -Recurse -Force $fullPath
        Write-Host "  Removed $dir" -ForegroundColor Gray
    }
}

Write-Host "Build artifacts cleaned." -ForegroundColor Green

# --- Step 4: Remove node_modules if present ---
$nodeModules = Join-Path $PSScriptRoot ".." "node_modules"
if (Test-Path $nodeModules) {
    Write-Host ""
    Write-Host "Removing node_modules..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $nodeModules
    Write-Host "node_modules removed." -ForegroundColor Green
}

# --- Done ---
Write-Host ""
Write-Host "=== Reset complete ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "To start fresh, run:"
Write-Host "  dotnet restore"
Write-Host "  dotnet build"
Write-Host "  dotnet test"
Write-Host ""
