# Changelog

All notable changes to this repository are documented in this file.

The format is based on Keep a Changelog principles.

## 2026-04-22 — Quality Review and Lab Hardening

### Quality Review Summary

A full lab-by-lab validation was performed from beginner and intermediate AI engineer perspectives. The changes below address the issues identified during that review to bring the workshop quality above 8/10 for both audiences.

### Fixed

- **deploy.yml build context mismatch (critical):** Changed ACR cloud build context from `./src/WorkshopLab.AgentHost` to `./src` in `.github/workflows/deploy.yml`. The Dockerfile copies both `WorkshopLab.Core` and `WorkshopLab.AgentHost`, so the build context must include both projects. This was already correct in `ci.yml` and the Lab 4 docs, but the deploy workflow used the wrong context and would fail on every run.

### Changed

- **Lab 0 prerequisites expanded:** Replaced the single-line prerequisite note with a full table showing every tool, which labs need it, and the verification command. Added a beginner tip clarifying that Docker and `azd` are not needed until Lab 4.
- **Lab 0 checkpoints added:** Inserted checkpoint callouts after `dotnet restore / build` (expect `0 Error(s)`) and after `dotnet run` (expect `Now listening on: http://localhost:8088`). Added guidance on where to find `AZURE_AI_PROJECT_ENDPOINT` and `MODEL_DEPLOYMENT_NAME` in the Foundry portal.
- **Lab 0 troubleshooting table added:** New table covering `dotnet` not found, auth errors, resource/deployment not found, and port conflicts.
- **Lab 4 Docker made optional:** Docker Desktop is no longer listed as a hard prerequisite. The prerequisites table now marks it as optional with a note that the lab uses `az acr build` (cloud build) by default. Step 1 Docker verification is also marked optional.
- **Lab 4 checkpoints added after key steps:**
  - Step 4 (Provision) — confirms all four `azd env get-values` outputs are present.
  - Step 6 (Publish) — confirms the ACR build succeeded and explains the correct build context.
  - Step 8 (Start) — confirms the agent reached `Running` / `Healthy` state.
- **Lab 4 troubleshooting table expanded:** Added rows for ACR build context errors, `SkuNotSupported` region failures, and container start 404 errors. Each row links to the specific issue in `knownissues.md`.
- **Lab 4 known-issues cross-link:** Added a footer link to `knownissues.md` at the end of the troubleshooting section so learners can find documented workarounds without searching.

### Added (Roadmap Items)

- **Glossary section in root README:** Added a table defining 8 key terms (hosted agent, prompt agent, agent.yaml, responses protocol, ACR, ACR cloud build, azd, deterministic tool) so beginners have a single reference.
- **Architecture diagram in root README:** Added an ASCII data-flow diagram showing Chat UI → Foundry Agent Service → Hosted Agent Container → Deterministic Tools, with lab numbers annotated on each component.
- **Lab 2 concrete code diff:** Replaced the vague "pick one tool to improve" instruction with a specific code snippet showing how to add a `full-stack` recommendation path in `RecommendImplementationShape`, plus a matching xUnit test and a validation curl/PowerShell command.
- **Lab 3 wording corrected:** Changed "Create a GitHub Actions workflow" to "Review and understand the GitHub Actions workflow" since `ci.yml` already exists in the repo.
- **Lab 4 Azure cost estimates:** Added an estimated-cost callout after the provision step (~$0.17/day for ACR Standard, under $1 total for a short workshop) with a link to the Azure pricing page.
- **macOS/Linux env-var syntax:** Added `export` command alternatives alongside every PowerShell `$env:` block in Labs 0, 2, 4, and 5.
- **Clean-state reset scripts:** Added `scripts/reset-workshop.ps1` (PowerShell) and `scripts/reset-workshop.sh` (bash) that tear down `azd` resources (with confirmation), clear workshop environment variables, and remove local build artifacts. Both support `--SkipAzure`/`--skip-azure` and `--Force`/`--force` flags.
- **Lab 1 inline checkpoint:** Added a checkpoint after step 6 that asks the learner to verify Copilot responds with xUnit-aware guidance, confirming the instructions file is loaded.
- **Lab 5 troubleshooting expanded:** Added 4 new rows covering token credential errors, CORS confusion, token expiry during long sessions, and connection refused when the hosted agent stops.
- **Progress indicators on every lab:** Added a "you are here" progress line (e.g. `Lab 0 → [Lab 1] → Lab 2 → …`) to the header of all six labs so beginners always know where they are in the sequence.

---

## 2026-03-31 — Initial Release of Lab

### Added

- Added Lab 5 UI guide at `labs/lab-5-ui/lab-5_readme.md`.
- Added new UI project `src/WorkshopLab.ChatUI` to the solution.
- Added screenshot automation scripts:
  - `scripts/capture-ui-chrome.mjs`
  - `scripts/capture-ui-chrome.ps1`
- Added npm scripts for screenshot capture in `package.json`:
  - `capture:screenshots`
  - `capture:screenshots:chrome`
- Added repository-level agent guidance file `AGENTS.md`.

### Changed

- Updated course maps and lab indexes to include Lab 5 in:
  - `README.md`
  - `labs/README.md`
- Updated Lab 5 docs to include screenshot references and beginner-friendly usage notes.
- Standardized screenshot naming for final response state:
  - `03-chat-ui-response-hd.png`

### Fixed

- Corrected Docker build context guidance for CI and lab instructions to use `./src` with explicit Dockerfile path.
- Added missing Authorization header guidance for Foundry production requests in `src/WorkshopLab.AgentHost/run-requests.http`.
- Added explicit local `/responses` validation command to Lab 0.

### Security and Public Sharing

- Sanitized environment-specific identifiers and replaced with placeholders across docs and metadata.
- Updated `.gitignore` for public sharing hygiene (`node_modules/`, IDE/OS artifacts, local debug outputs).
- Removed legacy screenshot naming artifact (`03-chat-ui-mobile.png`) in favor of `03-chat-ui-response-hd.png`.
