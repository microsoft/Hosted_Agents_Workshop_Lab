# Lab 0 — Core Guided: Set Up and Run a Hosted Agent

**Goal:** Open the repo in a dev container or Codespace, restore dependencies, configure Foundry environment variables, and validate the hosted agent locally.

**Time:** 25 minutes

**You will need:** .NET 10, Azure CLI, and access to a Microsoft Foundry project.

## Steps

1. Open the repository in VS Code or a Codespace.
2. Open `.devcontainer/devcontainer.json` and review the .NET 10 and Docker features.
3. Rebuild the container if prompted.
4. Restore and build the solution:

   ```powershell
   dotnet restore
   dotnet build
   ```

5. Set the required environment variables:

   ```powershell
   $env:AZURE_AI_PROJECT_ENDPOINT = "https://<resource>.services.ai.azure.com/api/projects/<project>"
   $env:MODEL_DEPLOYMENT_NAME = "gpt-4.1-mini"
   ```

6. Run the hosted agent:

   ```powershell
   dotnet run --project src/WorkshopLab.AgentHost
   ```

7. In a second terminal, send a test request to the local `/responses` endpoint:

   ```powershell
   Invoke-RestMethod -Method Post `
       -Uri "http://localhost:8088/responses" `
       -ContentType "application/json" `
       -Body '{"input":"We need an internal agent with private API access and workflow orchestration. Should we start with a hosted agent?"}'
   ```

8. Confirm that the agent answers as a Hosted Agent Readiness Coach.

**Expected result:** The hosted agent is reachable on `http://localhost:8088/responses` and responds with implementation guidance.