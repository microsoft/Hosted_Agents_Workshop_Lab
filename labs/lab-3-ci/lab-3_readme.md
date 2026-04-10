# Lab 3 — Core Guided: Add CI for Build, Test, and Container Validation

**Goal:** Create a GitHub Actions workflow that validates the Hosted Agent solution on every pull request.

**Time:** 30 minutes

**You will need:** Lab 2 completed.

## Steps

1. Open `.github/workflows/ci.yml`.
2. Verify the workflow installs Python dependencies and runs pytest.
3. Add a Docker build step that validates the agent container definition:

   ```yaml
   - name: Build hosted agent container
     run: docker build --platform linux/amd64 -t workshoplab-agent -f ./src/workshop_lab_agent_host/Dockerfile .
   ```

   > **Important:** The build context must be `.` (the repo root) because the Dockerfile copies `pyproject.toml`, `uv.lock`, and the `src/` packages. The `--file` flag points to the Dockerfile explicitly.

4. Ensure the workflow triggers on:
   - `workflow_dispatch`
   - push to `main`
   - pull requests targeting `main`
5. Commit the workflow to a feature branch.
6. Open a pull request and confirm the workflow passes.

**Expected result:** GitHub Actions validates source, tests, and Hosted Agent container packaging before merge.