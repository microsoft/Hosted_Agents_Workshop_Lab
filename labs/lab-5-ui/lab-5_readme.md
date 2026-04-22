# Lab 5 - Core Guided: Build a UI for the Hosted Agent

> **Progress:** Lab 5 of 5 — `Lab 0 → Lab 1 → Lab 2 → Lab 3 → Lab 4 → [Lab 5]`

**Goal:** Build a simple chat UI that calls your deployed Foundry hosted agent through the project-level `openai/v1/responses` endpoint.

**Time:** 30 minutes

**You will need:** Lab 4 completed.

---

## Step 1: Open the New UI Project

Open `src/WorkshopLab.ChatUI` in VS Code and review:

- `Program.cs` - app startup and service registration
- `Components/Pages/Home.razor` - chat interface
- `Services/FoundryAgentClient.cs` - Foundry API call logic

This project is a Blazor Web App that runs locally and forwards prompts to your deployed hosted agent.

---

## Step 2: Configure Foundry Settings

Set the Foundry endpoint and target agent name in `src/WorkshopLab.ChatUI/appsettings.Development.json`:

```json
{
  "Foundry": {
    "ProjectEndpoint": "https://<account>.services.ai.azure.com/api/projects/<project>",
    "AgentName": "hosted-agent-readiness-coach",
    "ApiVersion": "2025-01-01-preview"
  }
}
```

You can also provide the endpoint through environment variables:

```powershell
$env:AZURE_AI_PROJECT_ENDPOINT = "https://<account>.services.ai.azure.com/api/projects/<project>"
```

**macOS / Linux alternative:**

```bash
export AZURE_AI_PROJECT_ENDPOINT="https://<account>.services.ai.azure.com/api/projects/<project>"
```

---

## Step 3: Authenticate for Local Calls

The UI app uses `DefaultAzureCredential` and requests a token for `https://ai.azure.com/.default`.

Before running the app, make sure you are signed in:

```powershell
az login
```

If you use multiple tenants/subscriptions, set the active one first:

```powershell
az account set --subscription "<subscription-id-or-name>"
```

---

## Step 4: Run the UI App

From the repository root:

```powershell
dotnet restore
dotnet run --project src/WorkshopLab.ChatUI
```

Open the URL shown by ASP.NET Core (for example, `https://localhost:7xxx`).

### Reference screenshot: landing state

![Lab 5 chat UI landing](images/01-chat-ui-landing.png)

The landing state should show one assistant welcome message and a prefilled validation prompt.

---

## Step 5: Validate End-to-End Chat

In the chat box, send this prompt:

> We are onboarding a team to Microsoft Foundry hosted agents. We need private API access, a repeatable deployment process, and a launch checklist for production readiness. Should we use a hosted agent for this scenario, and what implementation shape do you recommend?

Expected behavior:

- Your message appears in the chat log
- The app calls `POST /openai/v1/responses` with `agent_reference`
- The assistant reply appears in the chat log
- The response includes a hosted-agent recommendation and implementation guidance

### Reference screenshot: prompt entered

![Lab 5 chat UI prompt entered](images/02-chat-ui-prompt-entered.png)

This view shows the prompt box ready to submit a checklist request.

---

## Step 6: Try Additional Prompts

Use at least two more prompts to validate reliability:

- `Create a launch checklist for an agent named triage-coach in the pilot environment.`
- `Our container starts, but requests to /responses fail. How should we troubleshoot that?`

Confirm the app remains responsive and returns scenario-aware answers.

### Reference screenshot: full-HD response state

![Lab 5 chat UI full-HD response](images/03-chat-ui-response-hd.png)

Use this as a quick visual check that the app renders correctly in full-screen desktop mode (1920x1080) and shows a successful assistant response.

### Optional: regenerate screenshots automatically

If you want to refresh all three screenshots after UI updates, run:

```powershell
dotnet run --project src/WorkshopLab.ChatUI --urls http://localhost:5075
```

In a second terminal:

```powershell
npm run capture:screenshots
```

This generates:

- `images/01-chat-ui-landing.png`
- `images/02-chat-ui-prompt-entered.png`
- `images/03-chat-ui-response-hd.png`

---

## Troubleshooting

| Problem | Fix |
|---|---|
| `Set Foundry:ProjectEndpoint...` error | Set `Foundry:ProjectEndpoint` in appsettings or `AZURE_AI_PROJECT_ENDPOINT` env var |
| `Foundry request failed with 401` | Run `az login` again and confirm tenant/subscription |
| `Foundry request failed with 404` | Verify `ProjectEndpoint` and `AgentName` values |
| Empty or unexpected response text | Check agent status and inspect raw response in the browser console/network trace |
| `TokenCredential authentication is not permitted` | Ensure `DefaultAzureCredential` can acquire a token. Run `az account get-access-token --resource "https://ai.azure.com"` to verify. |
| `CORS policy` error in browser console | The Blazor app calls the Foundry endpoint server-side, not from the browser. If you see CORS errors, confirm you are running the Blazor server (`dotnet run`) and not calling the Foundry API directly from JavaScript. |
| Token expires during long sessions | `DefaultAzureCredential` caches tokens. Restart the Blazor app or run `az login` again to refresh. |
| `HttpRequestException: Connection refused` | The hosted agent container may have stopped. Check its status in the Foundry portal or with `az cognitiveservices agent status`. |

---

## Expected Result

You now have a working end-to-end solution:

- Hosted agent deployed in Foundry
- UI client running locally in Blazor
- Real-time prompt/response flow through `openai/v1/responses` with `agent_reference`

This lab completes the full path from implementation and deployment to user-facing experience.
