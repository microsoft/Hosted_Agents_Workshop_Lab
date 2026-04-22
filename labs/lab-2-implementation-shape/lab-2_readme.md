# Lab 2 — Core Guided: Improve a Local Hosted Agent Tool

> **Progress:** Lab 2 of 5 — `Lab 0 → Lab 1 → [Lab 2] → Lab 3 → Lab 4 → Lab 5`

**Goal:** Extend the hosted agent by improving one deterministic local tool and verifying the change through tests and a local `/responses` call.

**Time:** 20 minutes

**You will need:** Lab 1 completed.

## Steps

1. Open `src/WorkshopLab.Core/HostedAgentAdvisor.cs`.
2. Review the existing tools:
   - `RecommendImplementationShape`
   - `BuildLaunchChecklist`
   - `TroubleshootHostedAgent`
3. Pick one tool to improve. The suggested change below adds a stronger recommendation path to `RecommendImplementationShape` for scenarios that require **all three** capabilities (custom code, tool access, and workflow orchestration) at the same time.

### Suggested code change

In `RecommendImplementationShape`, find the block that builds the recommendation string (right before the final `return`). Add a new condition that detects all three flags:

```csharp
// Add this block after the existing reason-building logic,
// before the final return statement:

if (requiresCode && requiresTools && requiresWorkflow)
{
    return string.Join(
        Environment.NewLine,
        [
            $"Recommended implementation: Hosted agent (full-stack)",
            $"Scenario goal: {goal}",
            "Why: the scenario requires custom code, external tool access, and multi-step orchestration — all three hosted-agent strengths.",
            "Suggested next step: start with a code-based hosted agent, register local tools for each integration, and add a workflow layer to orchestrate them."
        ]);
}
```

4. Update the deterministic logic in `HostedAgentAdvisor` with your change.
5. Add a matching test in `tests/WorkshopLab.Tests/HostedAgentAdvisorTests.cs`:

```csharp
[Fact]
public void RecommendImplementationShape_ReturnsFullStack_WhenAllThreeFlagsAreYes()
{
    var result = _advisor.RecommendImplementationShape(
        "Build an orchestrated pipeline with private APIs",
        "yes",
        "yes",
        "yes");

    Assert.Contains("full-stack", result);
    Assert.Contains("custom code, external tool access, and multi-step orchestration", result);
}
```

6. Run:

   ```powershell
   dotnet test
   ```

   > **Checkpoint:** All tests should pass, including your new test.

7. Start the hosted agent locally.
8. Send a request to `/responses` that exercises your updated tool logic:

   ```powershell
   Invoke-RestMethod -Method Post `
       -Uri "http://localhost:8088/responses" `
       -ContentType "application/json" `
       -Body '{"input":"We need an agent with custom code, private API integrations, and multi-step workflow orchestration. What implementation shape should we use?"}'
   ```

   **macOS / Linux alternative:**

   ```bash
   curl -X POST http://localhost:8088/responses \
     -H "Content-Type: application/json" \
     -d '{"input":"We need an agent with custom code, private API integrations, and multi-step workflow orchestration. What implementation shape should we use?"}'
   ```

9. Confirm the response matches the new deterministic behavior (you should see "full-stack" in the recommendation).

**Expected result:** A real feature change lands in the hosted agent and is covered by tests.