// Smoke-test a deployed Microsoft Foundry hosted agent via the Responses API.
//
// Reads a JSON catalog of prompts + assertions, POSTs each prompt to the agent's
// dedicated Responses endpoint, and validates the response text. The process exits
// non-zero if any test fails, so it works as a CI gate after deployment.
//
// Auth: uses the FOUNDRY_TOKEN environment variable when set (GitHub Actions),
// otherwise falls back to the local Azure CLI session
// (`az account get-access-token --resource https://ai.azure.com`).
//
// Endpoint contract (current hosted-agent spec):
//   POST {project_endpoint}/agents/{name}/endpoint/protocols/openai/responses
//        ?api-version=2025-11-15-preview
//   Header: Foundry-Features: HostedAgents=V1Preview
//   Body:   {"input": "<prompt>", "stream": false}

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

const string ApiVersion = "2025-11-15-preview";
const string TokenResource = "https://ai.azure.com";

// --- Parse arguments ---
string? projectEndpoint = null;
string? agentName = null;
string testsFile = "deployment/smoke-tests.json";
double timeoutSeconds = 120;

var queue = new Queue<string>(args);
while (queue.Count > 0)
{
    var arg = queue.Dequeue();
    switch (arg)
    {
        case "--project-endpoint":
            projectEndpoint = RequireValue(arg);
            break;
        case "--agent-name":
            agentName = RequireValue(arg);
            break;
        case "--tests-file":
            testsFile = RequireValue(arg);
            break;
        case "--timeout":
            timeoutSeconds = double.Parse(RequireValue(arg), CultureInfo.InvariantCulture);
            break;
        case "-h":
        case "--help":
            PrintUsage();
            return 0;
        default:
            Console.Error.WriteLine($"Unknown argument: {arg}");
            PrintUsage();
            return 2;
    }
}

string RequireValue(string flag) =>
    queue.Count > 0 ? queue.Dequeue() : throw new ArgumentException($"Missing value for {flag}");

if (string.IsNullOrWhiteSpace(projectEndpoint) || string.IsNullOrWhiteSpace(agentName))
{
    Console.Error.WriteLine("ERROR: --project-endpoint and --agent-name are required.");
    PrintUsage();
    return 2;
}

if (!File.Exists(testsFile))
{
    Console.Error.WriteLine($"ERROR: tests file not found: {testsFile}");
    return 2;
}

var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
Catalog catalog;
try
{
    catalog = JsonSerializer.Deserialize<Catalog>(await File.ReadAllTextAsync(testsFile), jsonOptions) ?? new Catalog();
}
catch (JsonException ex)
{
    Console.Error.WriteLine($"ERROR: could not parse {testsFile}: {ex.Message}");
    return 2;
}

var tests = catalog.Tests;

string token;
try
{
    token = GetToken();
}
catch (Exception ex)
{
    Console.Error.WriteLine(
        "ERROR: could not acquire a token. Set FOUNDRY_TOKEN or run 'az login'.\n" +
        $"       {ex.Message}");
    return 2;
}

Console.WriteLine($"Project endpoint : {projectEndpoint}");
Console.WriteLine($"Agent            : {agentName}");
Console.WriteLine($"Tests            : {tests.Count} from {testsFile}");
Console.WriteLine($"Per-req timeout  : {timeoutSeconds}s\n");

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
var responseIds = new Dictionary<string, string>();
var passed = 0;

foreach (var test in tests)
{
    var id = test.Id ?? "<unnamed>";
    var body = new Dictionary<string, object?>
    {
        ["input"] = test.Prompt,
        ["stream"] = false,
    };

    if (!string.IsNullOrEmpty(test.UsePreviousResponseId))
    {
        if (!responseIds.TryGetValue(test.UsePreviousResponseId, out var previousId))
        {
            Console.WriteLine($"  FAIL  {id}  (no saved response id '{test.UsePreviousResponseId}')");
            continue;
        }

        body["previous_response_id"] = previousId;
    }

    JsonElement payload;
    try
    {
        payload = await PostResponseAsync(body);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  FAIL  {id}  ({ex.Message})");
        continue;
    }

    var text = ExtractText(payload);

    if (!string.IsNullOrEmpty(test.SaveResponseIdAs) &&
        payload.TryGetProperty("id", out var idProp) &&
        idProp.ValueKind == JsonValueKind.String)
    {
        responseIds[test.SaveResponseIdAs] = idProp.GetString()!;
    }

    var failures = Evaluate(text, test.Assertions);
    if (failures.Count > 0)
    {
        Console.WriteLine($"  FAIL  {id}  -> {string.Join("; ", failures)}");
        Console.WriteLine($"        response: {Truncate(text, 300)}");
    }
    else
    {
        Console.WriteLine($"  PASS  {id}");
        passed++;
    }
}

Console.WriteLine($"\n=== Summary: {passed}/{tests.Count} passed ===");
return passed == tests.Count ? 0 : 1;

// --- Helpers ---

async Task<JsonElement> PostResponseAsync(Dictionary<string, object?> body)
{
    var url = $"{projectEndpoint!.TrimEnd('/')}/agents/{agentName}/endpoint/protocols/openai/responses" +
              $"?api-version={ApiVersion}";

    using var request = new HttpRequestMessage(HttpMethod.Post, url);
    request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");
    request.Headers.TryAddWithoutValidation("Foundry-Features", "HostedAgents=V1Preview");
    request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    using var response = await http.SendAsync(request);
    var content = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
    {
        throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {Truncate(content, 200)}");
    }

    using var document = JsonDocument.Parse(content);
    return document.RootElement.Clone();
}

static string GetToken()
{
    var fromEnv = Environment.GetEnvironmentVariable("FOUNDRY_TOKEN");
    if (!string.IsNullOrWhiteSpace(fromEnv))
    {
        return fromEnv.Trim();
    }

    var startInfo = new ProcessStartInfo
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
    };

    // `az` is a batch script on Windows, so invoke it through cmd.exe there.
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        startInfo.FileName = "cmd.exe";
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add("az");
    }
    else
    {
        startInfo.FileName = "az";
    }

    foreach (var argument in new[]
             {
                 "account", "get-access-token",
                 "--resource", TokenResource,
                 "--query", "accessToken", "-o", "tsv",
             })
    {
        startInfo.ArgumentList.Add(argument);
    }

    using var process = Process.Start(startInfo)
        ?? throw new InvalidOperationException("failed to start the Azure CLI ('az').");
    var stdout = process.StandardOutput.ReadToEnd();
    var stderr = process.StandardError.ReadToEnd();
    process.WaitForExit();

    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException($"az exited {process.ExitCode}: {stderr.Trim()}");
    }

    var value = stdout.Trim();
    if (string.IsNullOrEmpty(value))
    {
        throw new InvalidOperationException("az returned an empty token.");
    }

    return value;
}

static string ExtractText(JsonElement payload)
{
    if (payload.TryGetProperty("output_text", out var convenience) &&
        convenience.ValueKind == JsonValueKind.String)
    {
        var value = convenience.GetString();
        if (!string.IsNullOrEmpty(value))
        {
            return value;
        }
    }

    var parts = new List<string>();
    if (payload.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
    {
        foreach (var item in output.EnumerateArray())
        {
            if (item.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in content.EnumerateArray())
                {
                    if (element.TryGetProperty("text", out var textNode) &&
                        textNode.ValueKind == JsonValueKind.String)
                    {
                        parts.Add(textNode.GetString() ?? string.Empty);
                    }
                }
            }
        }
    }

    return string.Join("\n", parts);
}

static List<string> Evaluate(string text, Assertions? assertions)
{
    var failures = new List<string>();
    if (assertions is null)
    {
        return failures;
    }

    var lowered = text.ToLowerInvariant();

    if (assertions.ContainsAny is { Count: > 0 } anyTerms &&
        !anyTerms.Any(term => lowered.Contains(term.ToLowerInvariant())))
    {
        failures.Add($"contains_any [{string.Join(", ", anyTerms)}]");
    }

    if (assertions.ContainsAll is { Count: > 0 } allTerms)
    {
        var missing = allTerms.Where(term => !lowered.Contains(term.ToLowerInvariant())).ToList();
        if (missing.Count > 0)
        {
            failures.Add($"contains_all missing [{string.Join(", ", missing)}]");
        }
    }

    if (assertions.ContainsNone is { Count: > 0 } noneTerms)
    {
        var present = noneTerms.Where(term => lowered.Contains(term.ToLowerInvariant())).ToList();
        if (present.Count > 0)
        {
            failures.Add($"contains_none but found [{string.Join(", ", present)}]");
        }
    }

    return failures;
}

static string Truncate(string value, int max) =>
    value.Length <= max ? value : value[..max];

static void PrintUsage()
{
    Console.WriteLine(
        "Usage: dotnet run --project src/WorkshopLab.SmokeTests -- \\\n" +
        "         --project-endpoint <https://<account>.services.ai.azure.com/api/projects/<project>> \\\n" +
        "         --agent-name <hosted-agent-name> \\\n" +
        "         [--tests-file deployment/smoke-tests.json] \\\n" +
        "         [--timeout 120]");
}

// --- Models ---

internal sealed class Catalog
{
    [JsonPropertyName("tests")]
    public List<TestCase> Tests { get; set; } = [];
}

internal sealed class TestCase
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    [JsonPropertyName("assertions")]
    public Assertions? Assertions { get; set; }

    [JsonPropertyName("save_response_id_as")]
    public string? SaveResponseIdAs { get; set; }

    [JsonPropertyName("use_previous_response_id")]
    public string? UsePreviousResponseId { get; set; }
}

internal sealed class Assertions
{
    [JsonPropertyName("contains_any")]
    public List<string>? ContainsAny { get; set; }

    [JsonPropertyName("contains_all")]
    public List<string>? ContainsAll { get; set; }

    [JsonPropertyName("contains_none")]
    public List<string>? ContainsNone { get; set; }
}
