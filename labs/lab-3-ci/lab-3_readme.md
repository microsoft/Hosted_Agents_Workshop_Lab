# Lab 3 — Core Guided: Add CI for Build, Test, and Container Validation

> **Progress:** Lab 3 of 5 — `Lab 0 → Lab 1 → Lab 2 → [Lab 3] → Lab 4 → Lab 5`

**Goal:** Review and understand the GitHub Actions workflow that validates the Hosted Agent solution on every pull request, then verify it works.

**Time:** 30 minutes

**You will need:** Lab 2 completed.

## Steps

1. Open `.github/workflows/ci.yml` (this file already exists in the repo).
2. Review the workflow and confirm it restores, builds, and tests the solution.
3. Verify the Docker build step validates the agent container definition:

   ```yaml
   - name: Build hosted agent container
     run: docker build --platform linux/amd64 -t workshoplab-agent -f ./src/WorkshopLab.AgentHost/Dockerfile ./src
   ```

   > **Important:** The build context must be `./src` (not `./src/WorkshopLab.AgentHost`) because the Dockerfile copies both `WorkshopLab.Core` and `WorkshopLab.AgentHost`. The `--file` flag points to the Dockerfile explicitly.

4. Ensure the workflow triggers on:
   - `workflow_dispatch`
   - push to `main`
   - pull requests targeting `main`
5. Commit the workflow to a feature branch.
6. Open a pull request and confirm the workflow passes.

**Expected result:** GitHub Actions validates source, tests, and Hosted Agent container packaging before merge.