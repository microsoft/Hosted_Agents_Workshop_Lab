using System.Text;

namespace WorkshopLab.Core;

public sealed class HostedAgentAdvisor
{
    public string RecommendImplementationShape(
        string goal,
        string needsCustomCode,
        string needsExternalTools,
        string needsWorkflow)
    {
        var requiresCode = IsAffirmative(needsCustomCode);
        var requiresTools = IsAffirmative(needsExternalTools);
        var requiresWorkflow = IsAffirmative(needsWorkflow);

        var recommendation = requiresCode || requiresTools || requiresWorkflow
            ? "Hosted agent"
            : "Prompt agent";

        var reasons = new List<string>();

        if (requiresCode)
        {
            reasons.Add("custom server-side logic or enterprise integrations are required");
        }

        if (requiresTools)
        {
            reasons.Add("tool access or MCP connectivity is required");
        }

        if (requiresWorkflow)
        {
            reasons.Add("the scenario benefits from multi-step orchestration");
        }

        if (reasons.Count == 0)
        {
            reasons.Add("the scenario can start with lightweight prompting and does not need custom runtime logic yet");
        }

        return string.Join(
            Environment.NewLine,
            [
                $"Recommended implementation: {recommendation}",
                $"Scenario goal: {goal}",
                $"Why: {string.Join("; ", reasons)}.",
                recommendation == "Hosted agent"
                    ? "Suggested next step: create a code-based hosted agent with local tools first, then add project-specific connections once the /responses contract works locally."
                    : "Suggested next step: validate the assistant prompt first, then upgrade to a hosted agent when you need tools, stateful logic, or controlled integrations."
            ]);
    }

    public string BuildLaunchChecklist(string agentName, string environment)
    {
        var normalizedAgentName = string.IsNullOrWhiteSpace(agentName) ? "sample-hosted-agent" : agentName.Trim();
        var normalizedEnvironment = string.IsNullOrWhiteSpace(environment) ? "dev" : environment.Trim();

        var checklist = new[]
        {
            $"1. Confirm the agent name '{normalizedAgentName}' follows hosted-agent naming rules.",
            $"2. Create or verify the '{normalizedEnvironment}' environment variables: AZURE_AI_PROJECT_ENDPOINT and MODEL_DEPLOYMENT_NAME.",
            "3. Validate that agent.yaml declares kind 'hosted' and protocol 'responses' version 1.0.0.",
            "4. Run the agent locally and send a POST request to /responses before attempting any deployment.",
            "5. Make sure the Dockerfile exposes port 8088 and can build for linux/amd64.",
            "6. Add a CI check that restores, builds, and tests the solution on every pull request.",
            "7. Document one example request, one expected response pattern, and one troubleshooting path for the pilot team."
        };

        return string.Join(Environment.NewLine, checklist);
    }

    public string TroubleshootHostedAgent(string symptom)
    {
        var normalized = symptom?.Trim() ?? string.Empty;

        if (normalized.Contains("8088", StringComparison.OrdinalIgnoreCase) || normalized.Contains("responses", StringComparison.OrdinalIgnoreCase))
        {
            return "Check that the agent host is running, port 8088 is exposed, and you are sending a POST request to /responses with JSON content. Hosted agents should be validated locally before deployment.";
        }

        if (normalized.Contains("endpoint", StringComparison.OrdinalIgnoreCase) || normalized.Contains("credential", StringComparison.OrdinalIgnoreCase) || normalized.Contains("login", StringComparison.OrdinalIgnoreCase))
        {
            return "Verify AZURE_AI_PROJECT_ENDPOINT, confirm az login has access to the Foundry project, and ensure the project contains a usable OpenAI connection and deployment name.";
        }

        if (normalized.Contains("docker", StringComparison.OrdinalIgnoreCase) || normalized.Contains("amd64", StringComparison.OrdinalIgnoreCase) || normalized.Contains("container", StringComparison.OrdinalIgnoreCase))
        {
            return "Build the container for linux/amd64, confirm port 8088 is exposed, and keep the hosted-agent HTTP server as the default entrypoint.";
        }

        return "Start with the local validation path: confirm environment variables, run the host locally, test /responses, then move outward to Docker and Foundry deployment only after the local contract succeeds.";
    }

    private static bool IsAffirmative(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Trim().Equals("y", StringComparison.OrdinalIgnoreCase);
    }
}