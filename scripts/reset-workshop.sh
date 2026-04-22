#!/usr/bin/env bash
#
# Resets the workshop environment to a clean state for re-running labs.
#
# Tears down Azure resources provisioned by azd, clears environment variables,
# and removes local build artifacts. Useful for instructors resetting between
# sessions or learners starting over.
#
# Usage:
#   ./scripts/reset-workshop.sh                # interactive (asks before Azure teardown)
#   ./scripts/reset-workshop.sh --force        # skip confirmation prompts
#   ./scripts/reset-workshop.sh --skip-azure   # skip Azure teardown entirely

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

SKIP_AZURE=false
FORCE=false

for arg in "$@"; do
    case "$arg" in
        --skip-azure) SKIP_AZURE=true ;;
        --force) FORCE=true ;;
    esac
done

echo ""
echo "=== Workshop Clean-State Reset ==="
echo ""

# --- Step 1: Tear down Azure resources ---
if [ "$SKIP_AZURE" = false ]; then
    if command -v azd &>/dev/null; then
        echo "Checking for azd environment..."
        if azd env list 2>/dev/null | grep -q '\S'; then
            echo "Active azd environments found."

            if [ "$FORCE" = false ]; then
                echo ""
                echo "WARNING: This will permanently delete all Azure resources created by 'azd provision'."
                read -rp "Type 'yes' to continue, or anything else to skip Azure teardown: " confirm
                if [ "$confirm" != "yes" ]; then
                    echo "Skipping Azure teardown."
                    SKIP_AZURE=true
                fi
            fi

            if [ "$SKIP_AZURE" = false ]; then
                echo "Running azd down..."
                azd down --force --purge || echo "azd down returned a non-zero exit code. Check output above."
                echo "Azure resources removed."
            fi
        else
            echo "No azd environment found. Skipping Azure teardown."
        fi
    else
        echo "azd not installed. Skipping Azure teardown."
    fi
else
    echo "Azure teardown skipped (--skip-azure flag)."
fi

# --- Step 2: Clear environment variables ---
echo ""
echo "Clearing workshop environment variables..."

ENV_VARS=(
    AZURE_AI_PROJECT_ENDPOINT
    MODEL_DEPLOYMENT_NAME
    AZURE_CONTAINER_REGISTRY_NAME
    AZURE_CONTAINER_REGISTRY_ENDPOINT
    FOUNDRY_AGENT_ID
)

for var in "${ENV_VARS[@]}"; do
    if [ -n "${!var+x}" ]; then
        unset "$var"
        echo "  Cleared $var"
    fi
done

echo "Environment variables cleared."

# --- Step 3: Remove local build artifacts ---
echo ""
echo "Removing local build artifacts..."

ARTIFACT_DIRS=(
    "src/WorkshopLab.AgentHost/bin"
    "src/WorkshopLab.AgentHost/obj"
    "src/WorkshopLab.Core/bin"
    "src/WorkshopLab.Core/obj"
    "src/WorkshopLab.ChatUI/bin"
    "src/WorkshopLab.ChatUI/obj"
    "src/WorkshopLab.FoundryDeployment/bin"
    "src/WorkshopLab.FoundryDeployment/obj"
    "tests/WorkshopLab.Tests/bin"
    "tests/WorkshopLab.Tests/obj"
)

for dir in "${ARTIFACT_DIRS[@]}"; do
    full_path="$REPO_ROOT/$dir"
    if [ -d "$full_path" ]; then
        rm -rf "$full_path"
        echo "  Removed $dir"
    fi
done

echo "Build artifacts cleaned."

# --- Step 4: Remove node_modules if present ---
if [ -d "$REPO_ROOT/node_modules" ]; then
    echo ""
    echo "Removing node_modules..."
    rm -rf "$REPO_ROOT/node_modules"
    echo "node_modules removed."
fi

# --- Done ---
echo ""
echo "=== Reset complete ==="
echo ""
echo "To start fresh, run:"
echo "  dotnet restore"
echo "  dotnet build"
echo "  dotnet test"
echo ""
