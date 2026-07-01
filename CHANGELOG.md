# Changelog

All notable changes to this repository are documented in this file.

The format is based on Keep a Changelog principles.

## 2026-07-01 — .NET smoke test, lab navigation, and cleanup guardrail

### Changed

- **Smoke test rewritten in .NET.** Replaced the Python `deployment/smoke-tests.py` with a .NET console runner at `src/WorkshopLab.SmokeTests` (run via `dotnet run`), so the whole workshop is a single language. Same `deployment/smoke-tests.json` catalog and behavior (`FOUNDRY_TOKEN`/`az` auth, `contains_any/all/none` assertions, `previous_response_id` threading, non-zero exit on failure). The `.github/actions/smoke-test` composite action now sets up .NET and runs the project; Lab 4 documents `dotnet run` instead of `python`.
- **README leads with `azd ai agent`** as the primary deployment path; the manual ACR/SDK route is presented as optional background.
- **Sequential navigation across the labs** — each lab ends with a `◀ Previous · 📚 All labs · Next ▶` footer, and the README/labs index link into the sequence, so readers can follow the whole path without jumping between files. Lab 4 gained a "Two ways to deploy" comparison box.

### Fixed

- **Premature teardown guardrail.** Moved the `azd down` cleanup from Lab 4 to the end of Lab 5 and added a stop note in Lab 4, so learners don't delete the deployed agent that Lab 5's UI needs before finishing the workshop.

## 2026-07-01 — Live deployment validation, non-deprecated model, modern infra, and smoke tests

### Changed

- **Model updated to `gpt-5.4-mini`** (GA, non-deprecated) across code and docs — `gpt-4.1-mini` is in the deprecating lifecycle. Verified available with quota in `northcentralus`.
- **Replaced the legacy ACR-only Bicep with the modern `azd-ai-starter-basic` infrastructure** under `infra/` (Foundry account + project + model deployment + ACR + App Insights + Log Analytics via Azure Verified Modules). `azd provision` now creates the whole Foundry environment; the old `SkuNotSupported` ACR failure is gone.
- **`azure.yaml` / `agent.yaml` finalized for the azd container path:** removed the `image: ${AGENT_IMAGE}` placeholder (azd builds from the Dockerfile) and set `docker.context: ../` so the shared `WorkshopLab.Core` project is included in the build.

### Fixed

- **Runtime `TypeLoadException` on the deployed agent.** The `Microsoft.Extensions.AI*` pins had drifted to 10.7.0, which removed `UserInputRequestContent` referenced by `Azure.AI.AgentServer` beta.11. Re-pinned to 10.3.0 and added Dependabot `ignore` rules (`>=10.4.0`) so it cannot regress.
- **`401 PermissionDenied` on invoke.** Documented and applied the agent managed-identity roles (**Cognitive Services OpenAI User** + **Azure AI Developer**) required by this workshop's custom-code model-connection pattern.

### Added

- **Smoke tests** (based on the [Foundry smoke-test blog](https://techcommunity.microsoft.com/blog/azuredevcommunityblog/smoke-test-microsoft-foundry-agents-with-github-actions/4531912)): `deployment/smoke-tests.py`, `deployment/smoke-tests.json` (adapted to the Readiness Coach), a reusable `.github/actions/smoke-test/action.yml`, and the `.github/workflows/smoke-test.yml` dispatch/`workflow_call` workflow. Validated 5/5 against the live agent, including `previous_response_id` chaining.
- **Lab 0 "Required Azure permissions"** — operator roles + agent managed-identity roles, each with the reason, plus hosted-agent security best practices.
- **Lab 4 Step 9 (optional smoke test)** with local + GitHub Actions setup instructions, and a `401` troubleshooting row.
- **`global.json`** pinning the .NET SDK (10.0.100, `rollForward: latestMinor`).

### Validated live

- End-to-end on the **`azd ai agent`** pathway in `northcentralus`: `azd ai agent init` → `azd provision` → `azd deploy` → `azd ai agent invoke` returned a correct, tool-backed response from `gpt-5.4-mini`. Build clean (0 warnings/0 errors); tests 6/6; smoke tests 5/5.

## 2026-07-01 — Add global.json to harden the .NET SDK requirement

### Fixed

- **Added [global.json](global.json)** pinning the SDK to `10.0.100` with `rollForward: latestMinor`. This addresses `knownissues.md` Issue 1: an older SDK (8.x/9.x) now fails immediately with a clear message instead of confusing language/framework errors, and any installed 10.x SDK is accepted for reproducible builds. Verified with SDK 10.0.109 — solution builds with 0 warnings, 0 errors.
- Reviewed the remaining known issues: Issue 7 is fixed, Issue 1 is now hardened, and Issues 2–6 are environmental (Azure region/SKU, RBAC, CLI version, manual build context) rather than repo defects, so they remain as documented workarounds. Added a note to `knownissues.md` clarifying this.

## 2026-07-01 — Clear the OpenTelemetry NU1902 advisories

### Fixed

- **Pinned the OpenTelemetry stack to `1.16.0`** in [WorkshopLab.AgentHost.csproj](src/WorkshopLab.AgentHost/WorkshopLab.AgentHost.csproj), clearing the two moderate `NU1902` advisories that arrived transitively: `OpenTelemetry.Api` 1.13.1 (GHSA-g94r-2vxg-569j) and `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.12.0 (GHSA-4625-4j76-fww9). The OpenTelemetry packages ship as a coherent set, so `OpenTelemetry`, `OpenTelemetry.Api`, `OpenTelemetry.Api.ProviderBuilderExtensions`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`, and `OpenTelemetry.Extensions.Hosting` were moved together. The full solution now builds with **0 warnings, 0 errors**; all 6 tests pass. Runtime telemetry export should still be spot-checked during the live Foundry test.

## 2026-07-01 — Migrate WorkshopLab.FoundryDeployment to the Azure.AI.Projects.Agents SDK

### Changed

- **Replaced the reflection-based deployment helper with the typed `Azure.AI.Projects.Agents` SDK.** [src/WorkshopLab.FoundryDeployment/Program.cs](src/WorkshopLab.FoundryDeployment/Program.cs) now uses `AgentAdministrationClient.CreateAgentVersion(...)` with a strongly-typed `HostedAgentDefinition` (`ProjectsAgentDefinition.CreateHostedAgentDefinition` + `ContainerConfiguration` + `ProtocolVersionRecord`), passing the preview `Foundry-Features: HostedAgents=V1Preview` header via the `foundryFeatures` parameter. Removed all `System.Reflection` calls into private SDK members and the `AAIP001`-suppressed experimental surface is now used directly. This resolves the reflection follow-up noted below.
- **Added the `Azure.AI.Projects.Agents` `2.1.0-beta.4` package** to [WorkshopLab.FoundryDeployment.csproj](src/WorkshopLab.FoundryDeployment/WorkshopLab.FoundryDeployment.csproj) (matching the existing `Azure.AI.Projects` beta).
- **New helper CLI contract.** The app accepts structured flags (`--image`, `--cpu`, `--memory`, `--protocol`, `--protocol-version`, `--env NAME=VALUE`) or a declarative `--manifest` with `--set` placeholder substitution. Both build the same typed definition. Updated [scripts/deploy-hosted-agent-direct.ps1](scripts/deploy-hosted-agent-direct.ps1) (structured flags) and [scripts/deploy-foundry-agent.ps1](scripts/deploy-foundry-agent.ps1) (manifest + `--agent-name`; dropped the removed `--agent-id`, since `CreateAgentVersion` creates the next version automatically).
- Build clean (0 errors; the previous `CS8600` warning is gone with the reflection code); all 6 tests pass. The typed SDK usage is compiler-verified, though a live Foundry project is still needed to validate the deploy end to end.

## 2026-07-01 — Make `azd ai agent` the primary Lab 4 path; manual route becomes optional appendix

### Changed

- **Lab 4 now leads with the `azd ai agent` extension** (`init --from-code` → `run` → `provision` → `deploy` → `invoke`), matching the current Microsoft-recommended hosted-agent workflow. Rewrote [labs/lab-4-deploy/lab-4_readme.md](labs/lab-4-deploy/lab-4_readme.md) around the extension lifecycle, container deploy mode (the shared `WorkshopLab.Core` reference means the existing Dockerfile is reused), and a Docker build-context note.
- **The former manual flow moved to [labs/lab-4-deploy/lab-4-appendix-manual-deploy.md](labs/lab-4-deploy/lab-4-appendix-manual-deploy.md)** — ACR provisioning, `az acr build`, `WorkshopLab.FoundryDeployment`, and `az cognitiveservices agent start` — clearly marked **optional / background only, not required** to finish the workshop.
- **`src/WorkshopLab.AgentHost/Program.cs` reads both env-var conventions.** Endpoint resolves from `FOUNDRY_PROJECT_ENDPOINT` (platform-injected by `azd ai agent`) with fallback to `AZURE_AI_PROJECT_ENDPOINT` (manual path); model resolves from `AZURE_AI_MODEL_DEPLOYMENT_NAME` with fallback to `MODEL_DEPLOYMENT_NAME`. This lets the same container run under both paths.
- **Deploy scripts flagged as optional.** Added header comments to [scripts/deploy-hosted-agent-direct.ps1](scripts/deploy-hosted-agent-direct.ps1) and [scripts/deploy-foundry-agent.ps1](scripts/deploy-foundry-agent.ps1) noting they belong to the manual appendix.
- Updated the course map in [README.md](README.md) and the lab index in [labs/README.md](labs/README.md).
- Build clean (0 errors); all 6 tests pass.

## 2026-07-01 — Align hosted-agent definition and invocation with the current Foundry spec

### Fixed

- **Agent invocation used the superseded initial-preview pattern.** The Blazor UI (`FoundryAgentClient`), the `run-requests.http` production requests, and Labs 4–5 all invoked the agent through the shared project endpoint `POST {project}/openai/v1/responses` with an `agent_reference` in the body. Per [Migrate hosted agents to the latest version](https://learn.microsoft.com/azure/foundry/agents/how-to/migrate-hosted-agent-preview), each hosted agent now has a **dedicated endpoint** and is invoked at `POST {project}/agents/{name}/endpoint/protocols/openai/responses?api-version=2025-11-15-preview` with the `Foundry-Features: HostedAgents=V1Preview` header and a plain `input` body (no `agent_reference`). Updated the client, the `.http` samples, and the labs.
- **Hosted agent definition JSON used out-of-date field names.** `scripts/deploy-hosted-agent-direct.ps1` posted a top-level `image` plus `container_protocol_versions` at version `v1`. Updated to the current `HostedAgentDefinition` shape: `container_configuration.image` and `protocol_versions` at version `1.0.0`.
- **Protocol version `v1` → `1.0.0`** across `agent.yaml`, both deploy scripts, `HostedAgentAdvisor`, and Lab 4, matching the [agent.yaml schema reference](https://learn.microsoft.com/azure/foundry/agents/concepts/agent-yaml-reference).
- **Missing `knownissues.md`.** Lab 4 linked to `knownissues.md` (Issues 2, 3, 6, and a full-list footer) but the file did not exist. Added it with numbered, copy-paste workarounds.

### Changed

- **`api-version` bumped `2025-01-01-preview` → `2025-11-15-preview`** in `appsettings.json`, `FoundryAgentClient`, `run-requests.http`, and Labs 4–5.
- **`agent.yaml` manifest now references the container image** via a new `image: ${AGENT_IMAGE}` field, and `scripts/deploy-foundry-agent.ps1` accepts `-ImageUri` to fill it in, closing the gap where the manifest deploy path had no image reference.
- Build verified clean (0 errors) and all 6 tests pass.

### Not changed (recommended follow-ups)

- The `NU1902` OpenTelemetry advisories remain (transitive via the agent-server package). Tracked in `knownissues.md` (Issue 7).

## 2026-06-02 — Pin Microsoft.Extensions.AI to 10.3.0 in AgentHost and refresh peripheral packages

### Fixed

- **Agent host throws `TypeLoadException: Could not load type 'UserInputRequestContent' from assembly 'Microsoft.Extensions.AI.Abstractions'` on the first `/responses` request.** `Azure.AI.AgentServer.AgentFramework 1.0.0-beta.11` depends on `Microsoft.Agents.AI 1.0.0-rc3`, which was built against `Microsoft.Extensions.AI.Abstractions 10.3.0`. The previous `Microsoft.Extensions.AI.OpenAI 10.6.0` reference forced the Abstractions assembly up to 10.6.0, which removed `UserInputRequestContent` in the 10.4+ refactor of request content types. Pinned `Microsoft.Extensions.AI`, `Microsoft.Extensions.AI.Abstractions`, and `Microsoft.Extensions.AI.OpenAI` to `10.3.0` in `src/WorkshopLab.AgentHost/WorkshopLab.AgentHost.csproj` so the runtime graph matches the agent server beta. Revisit when `Azure.AI.AgentServer.AgentFramework` ships a build rebuilt against `Microsoft.Agents.AI 1.8.x` / Abstractions 10.6.x.

### Changed

- **Refreshed unconstrained packages to latest.** Packages outside the agent-server compatibility chain were bumped to current versions:
  - `Azure.AI.OpenAI` 2.8.0-beta.1 → **2.9.0-beta.1** (AgentHost)
  - `Azure.AI.Projects` 2.0.1 → **2.1.0-beta.3** (AgentHost, FoundryDeployment)
  - `Microsoft.NET.Test.Sdk` 18.5.1 → **18.6.0** (Tests)
  - `Azure.Identity` 1.21.0, `YamlDotNet` 18.0.0, `xunit` 2.9.3, `xunit.runner.visualstudio` 3.1.5, `coverlet.collector` 10.0.1 are already at the latest stable.
- Build verified clean (5 build warnings, all pre-existing: 2× `NU1902` OpenTelemetry CVE notices, 1× `CS8600` in `FoundryDeployment/Program.cs`). All 6 tests still pass. `Microsoft.Extensions.AI.Abstractions` still resolves to `10.3.0` in `project.assets.json` after the refresh.

### Why other packages were not updated

`Azure.AI.AgentServer.AgentFramework 1.0.0-beta.11` is the newest release and hard-pins:

- `Azure.AI.AgentServer.Core` to `[1.0.0-beta.11]` (later beta.21–beta.25 builds of Core are not usable on their own)
- `Microsoft.Agents.AI 1.0.0-rc3` (the stable 1.0.0 → 1.8.0 line on NuGet cannot be substituted)

Microsoft.Agents.AI rc3 was compiled against `Microsoft.Extensions.AI.Abstractions 10.3.0`, so anything that drags Abstractions above 10.3.x removes `UserInputRequestContent` and breaks the hosted-agent runtime. Until a newer `Azure.AI.AgentServer.AgentFramework` beta ships rebuilt against `Microsoft.Agents.AI 1.x` stable, these three versions must stay pinned.

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
