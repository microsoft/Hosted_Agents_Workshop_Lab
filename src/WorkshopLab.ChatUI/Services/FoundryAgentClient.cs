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

    public async Task<string> SendAsync(string userPrompt, CancellationToken cancellationToken = default)
    {
        var endpoint = ResolveProjectEndpoint();
        var agentName = configuration["Foundry:AgentName"] ?? "hosted-agent-readiness-coach";
        var apiVersion = configuration["Foundry:ApiVersion"] ?? "2025-11-15-preview";

        var token = await _credential.GetTokenAsync(TokenScope, cancellationToken);

        // Hosted agents are invoked through their dedicated agent endpoint using the
        // OpenAI Responses protocol. The platform manages conversation history, so the
        // request body is just the input; no agent_reference is required.
        var requestUri = $"{endpoint}/agents/{agentName}/endpoint/protocols/openai/responses?api-version={apiVersion}";

        var payload = new
        {
            input = userPrompt,
            stream = false
        };

        var raw = await SendWithRetryAsync(requestUri, token.Token, payload, cancellationToken);
        return ExtractAssistantText(raw);
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
        var endpoint = configuration["Foundry:ProjectEndpoint"]
            ?? configuration["AZURE_AI_PROJECT_ENDPOINT"];

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException(
                "Set Foundry:ProjectEndpoint (or AZURE_AI_PROJECT_ENDPOINT) before using the UI.");
        }

        return endpoint.TrimEnd('/');
    }

    private static string ExtractAssistantText(string responseJson)
    {
        using var doc = JsonDocument.Parse(responseJson);

        if (!doc.RootElement.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
        {
            return responseJson;
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

        return responseJson;
    }
}
