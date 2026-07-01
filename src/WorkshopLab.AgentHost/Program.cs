using System.ClientModel.Primitives;
using System.ComponentModel;
using Azure.AI.AgentServer.AgentFramework.Extensions;
using Azure.AI.OpenAI;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using WorkshopLab.Core;

// Endpoint resolution supports both deployment paths:
//   • azd ai agent  → the platform injects FOUNDRY_PROJECT_ENDPOINT into the container
//   • manual deploy → AZURE_AI_PROJECT_ENDPOINT is set on the agent definition
var projectEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
	?? Environment.GetEnvironmentVariable("AZURE_AI_PROJECT_ENDPOINT")
	?? throw new InvalidOperationException("Set FOUNDRY_PROJECT_ENDPOINT or AZURE_AI_PROJECT_ENDPOINT.");

// Model deployment name: AZURE_AI_MODEL_DEPLOYMENT_NAME is the azd convention;
// MODEL_DEPLOYMENT_NAME is used by the manual deploy path.
var deploymentName = Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME")
	?? Environment.GetEnvironmentVariable("MODEL_DEPLOYMENT_NAME")
	?? "gpt-5.4-mini";

Console.WriteLine($"WorkshopLab Agent Host starting for project: {projectEndpoint}");
Console.WriteLine($"Using deployment: {deploymentName}");

var credential = new DefaultAzureCredential();
var projectClient = new AIProjectClient(new Uri(projectEndpoint), credential);
var connection = projectClient.GetConnection(typeof(AzureOpenAIClient).FullName!);

if (!connection.TryGetLocatorAsUri(out Uri? openAiEndpoint) || openAiEndpoint is null)
{
	throw new InvalidOperationException("Failed to resolve the Azure OpenAI connection from the Foundry project.");
}

openAiEndpoint = new Uri($"https://{openAiEndpoint.Host}");

var chatClient = new AzureOpenAIClient(openAiEndpoint, credential)
	.GetChatClient(deploymentName)
	.AsIChatClient()
	.AsBuilder()
	.UseOpenTelemetry(sourceName: "WorkshopLab.Agent", configure: options => options.EnableSensitiveData = false)
	.Build();

var advisor = new HostedAgentAdvisor();

[Description("Recommend whether a team should start with a hosted agent and explain the implementation shape to use.")]
string RecommendImplementationShape(
	[Description("The business goal for the agent.")] string goal,
	[Description("Whether the team needs custom server-side code such as deterministic logic or enterprise integrations. Use yes or no.")] string needsCustomCode,
	[Description("Whether the team needs external tools, MCP integrations, or private APIs. Use yes or no.")] string needsExternalTools,
	[Description("Whether the team expects multi-step orchestration or workflow handoffs. Use yes or no.")] string needsWorkflow)
{
	return advisor.RecommendImplementationShape(goal, needsCustomCode, needsExternalTools, needsWorkflow);
}

[Description("Create a practical launch checklist for a Microsoft Foundry Hosted Agent pilot.")]
string BuildLaunchChecklist(
	[Description("The short agent name.")] string agentName,
	[Description("The target environment such as dev, pilot, or production.")] string environment)
{
	return advisor.BuildLaunchChecklist(agentName, environment);
}

[Description("Suggest troubleshooting guidance for a common hosted-agent symptom.")]
string TroubleshootHostedAgent(
	[Description("A short symptom or error description from the team.")] string symptom)
{
	return advisor.TroubleshootHostedAgent(symptom);
}

var agent = new ChatClientAgent(
	chatClient,
	name: "HostedAgentReadinessCoach",
	instructions:
	"""
	You are a Microsoft Foundry Hosted Agent readiness coach.

	Help teams choose when to use a hosted agent, prepare a safe pilot, and troubleshoot early setup issues.

	When a user asks for implementation guidance:
	1. Clarify the business goal when it is vague.
	2. Use RecommendImplementationShape for architecture decisions.
	3. Use BuildLaunchChecklist for concrete onboarding steps.
	4. Use TroubleshootHostedAgent for setup or runtime symptoms.
	5. Keep answers practical, concise, and aimed at teams getting started with Microsoft Foundry Hosted Agents.
	""",
	tools:
	[
		AIFunctionFactory.Create(RecommendImplementationShape),
		AIFunctionFactory.Create(BuildLaunchChecklist),
		AIFunctionFactory.Create(TroubleshootHostedAgent)
	])
	.AsBuilder()
	.UseOpenTelemetry(sourceName: "WorkshopLab.Agent", configure: options => options.EnableSensitiveData = false)
	.Build();

Console.WriteLine("Hosted Agent Readiness Coach is listening on http://localhost:8088");

await agent.RunAIAgentAsync(telemetrySourceName: "WorkshopLab.Agent");
