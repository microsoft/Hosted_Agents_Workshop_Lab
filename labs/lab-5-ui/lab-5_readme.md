# Lab 5 - Core Guided: Build a UI for the Hosted Agent

**Goal:** Build a simple chat UI that calls your deployed Foundry hosted agent through the project-level `openai/v1/responses` endpoint.

**Time:** 30 minutes

**You will need:** Lab 4 completed.

---

## Step 1: Open the New UI Project

Open `src/workshop_lab_chat_ui` in VS Code and review:

- `app.py` - Flask app startup, routes, and Foundry API call logic
- `templates/index.html` - chat interface
- `static/app.css` - UI styles

This project is a Flask web application that runs locally and forwards prompts to your deployed hosted agent.

---

## Step 2: Configure Foundry Settings

Set the Foundry endpoint and target agent name via environment variables:

**Windows (PowerShell):**

```powershell
$env:FOUNDRY_PROJECT_ENDPOINT = "https://<account>.services.ai.azure.com/api/projects/<project>"
$env:FOUNDRY_AGENT_NAME = "hosted-agent-readiness-coach"
$env:FOUNDRY_API_VERSION = "2025-01-01-preview"
```

**macOS / Linux:**

```bash
export FOUNDRY_PROJECT_ENDPOINT="https://<account>.services.ai.azure.com/api/projects/<project>"
export FOUNDRY_AGENT_NAME="hosted-agent-readiness-coach"
export FOUNDRY_API_VERSION="2025-01-01-preview"
```

Alternatively, you can use `AZURE_AI_PROJECT_ENDPOINT`:

**Windows (PowerShell):**

```powershell
$env:AZURE_AI_PROJECT_ENDPOINT = "https://<account>.services.ai.azure.com/api/projects/<project>"
```

**macOS / Linux:**

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
uv run python src/workshop_lab_chat_ui/app.py
```

Open `http://localhost:5075` in your browser.

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
uv run python src/workshop_lab_chat_ui/app.py
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
| `AZURE_AI_PROJECT_ENDPOINT` not set error | Set `FOUNDRY_PROJECT_ENDPOINT` or `AZURE_AI_PROJECT_ENDPOINT` env var |
| `Foundry request failed with 401` | Run `az login` again and confirm tenant/subscription |
| `Foundry request failed with 404` | Verify endpoint and `FOUNDRY_AGENT_NAME` values |
| Empty or unexpected response text | Check agent status and inspect raw response in the browser console/network trace |

---

## Expected Result

You now have a working end-to-end solution:

- Hosted agent deployed in Foundry
- UI client running locally in Flask
- Real-time prompt/response flow through `openai/v1/responses` with `agent_reference`

This lab completes the full path from implementation and deployment to user-facing experience.
