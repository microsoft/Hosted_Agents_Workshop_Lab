using System.Net.Http.Headers;
using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;

namespace WorkshopLab.ChatUI.Services;

public sealed class FoundryAgentClient(IConfiguration configuration, IHttpClientFactory httpClientFactory)
{
    private static readonly TokenRequestContext TokenScope = new(["https://ai.azure.com/.default"]);
    private readonly TokenCredential _credential = new DefaultAzureCredential();

    public async Task<AgentReply> SendAsync(
        string userPrompt,
        string? previousResponseId = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = ResolveProjectEndpoint();
        var agentName = configuration["Foundry:AgentName"] ?? "hosted-agent-readiness-coach";
        var apiVersion = configuration["Foundry:ApiVersion"] ?? "v1";

        var token = await _credential.GetTokenAsync(TokenScope, cancellationToken);

        // Hosted agents are invoked through their dedicated agent endpoint using the
        // OpenAI Responses protocol. Passing previous_response_id threads the turns
        // together so the agent remembers earlier messages; it is null on the first turn.
        var requestUri = $"{endpoint}/agents/{agentName}/endpoint/protocols/openai/responses?api-version={apiVersion}";

        object payload = previousResponseId is null
            ? new { input = userPrompt, stream = false }
            : new { input = userPrompt, stream = false, previous_response_id = previousResponseId };

        var raw = await SendWithRetryAsync(requestUri, token.Token, payload, cancellationToken);
        return ParseReply(raw);
    }

    private async Task<string> SendWithRetryAsync(
        string requestUri,
        string bearerToken,
        object payload,
        CancellationToken cancellationToken)
    {
        Exception? lastError = null;

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                return await SendOnceAsync(requestUri, bearerToken, payload, cancellationToken);
            }
            catch (HttpRequestException ex) when (attempt == 1)
            {
                lastError = ex;
                await Task.Delay(500, cancellationToken);
            }
            catch (TaskCanceledException ex) when (attempt == 1)
            {
                lastError = ex;
                await Task.Delay(500, cancellationToken);
            }
        }

        throw new InvalidOperationException(
            $"Foundry request failed after retry: {lastError?.Message}",
            lastError);
    }

    private async Task<string> SendOnceAsync(
        string requestUri,
        string bearerToken,
        object payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        request.Headers.TryAddWithoutValidation("Foundry-Features", "HostedAgents=V1Preview");
        request.Version = HttpVersion.Version11;
        request.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(45));

        using var client = httpClientFactory.CreateClient();
        using var response = await client.SendAsync(request, timeoutCts.Token);
        var raw = await response.Content.ReadAsStringAsync(timeoutCts.Token);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Foundry request failed with {(int)response.StatusCode}: {raw}");
        }

        return raw;
    }

    private string ResolveProjectEndpoint()
    {
        // Prefer an explicitly configured project endpoint, then the azd-style env var.
        // Ignore blank values and the shipped placeholder (which contains '<') so a stale
        // appsettings entry can't shadow a real AZURE_AI_PROJECT_ENDPOINT and cause
        // "Invalid URI: The hostname could not be parsed".
        foreach (var candidate in new[]
        {
            configuration["Foundry:ProjectEndpoint"],
            configuration["AZURE_AI_PROJECT_ENDPOINT"]
        })
        {
            if (string.IsNullOrWhiteSpace(candidate) || candidate.Contains('<'))
            {
                continue;
            }

            if (!Uri.TryCreate(candidate.Trim(), UriKind.Absolute, out _))
            {
                continue;
            }

            return candidate.Trim().TrimEnd('/');
        }

        throw new InvalidOperationException(
            "No valid Foundry project endpoint is configured. Set Foundry:ProjectEndpoint in " +
            "appsettings.Development.json, or set the AZURE_AI_PROJECT_ENDPOINT (or Foundry__ProjectEndpoint) " +
            "environment variable to your project endpoint, for example " +
            "https://<account>.services.ai.azure.com/api/projects/<project>.");
    }

    private static AgentReply ParseReply(string responseJson)
    {
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        var responseId = root.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String
            ? idElement.GetString()
            : null;

        return new AgentReply(ExtractAssistantText(root) ?? responseJson, responseId);
    }

    private static string? ExtractAssistantText(JsonElement root)
    {
        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("type", out var itemType) || itemType.GetString() != "message")
            {
                continue;
            }

            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var part in content.EnumerateArray())
            {
                if (!part.TryGetProperty("type", out var partType) || partType.GetString() != "output_text")
                {
                    continue;
                }

                if (part.TryGetProperty("text", out var text))
                {
                    return text.GetString() ?? string.Empty;
                }
            }
        }

        return null;
    }
}

/// <summary>The assistant's reply text plus the response id used to thread the next turn.</summary>
public sealed record AgentReply(string Text, string? ResponseId);
