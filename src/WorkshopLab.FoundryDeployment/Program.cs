// Hosted-agent deployment helper — OPTIONAL / background path.
//
// Registers a hosted-agent version in Microsoft Foundry from a prebuilt container
// image using the Azure.AI.Projects.Agents SDK (AgentAdministrationClient). The
// recommended path for real work is the `azd ai agent` extension — see
// labs/lab-4-deploy/lab-4_readme.md. This tool backs the manual appendix.
//
// Two input modes:
//   1. Structured flags:  --image <uri> [--cpu 1] [--memory 2Gi]
//                         [--protocol responses] [--protocol-version 1.0.0]
//                         [--env NAME=VALUE ...]
//   2. Declarative manifest:  --manifest <agent.yaml> [--set NAME=VALUE ...]
//      (--set values are substituted into ${NAME} / {{NAME}} placeholders before parsing)
//
// Explicit flags override values read from the manifest.
#pragma warning disable AAIP001 // Hosted agents are an experimental preview feature.

using Azure.AI.Projects.Agents;
using Azure.Identity;
using YamlDotNet.Serialization;

var arguments = ParseArguments(args);

string projectEndpoint = GetRequired(arguments, "project-endpoint", "AZURE_AI_PROJECT_ENDPOINT");
string agentName = GetOptional(arguments, "agent-name", null)
    ?? throw new InvalidOperationException("Missing required '--agent-name'.");
string? manifestPath = GetOptional(arguments, "manifest", null);
string foundryFeatures = GetOptional(arguments, "foundry-features", null) ?? "HostedAgents=V1Preview";

// Nullable so we can tell "not supplied" from "supplied": manifest fills gaps, flags win.
string? image = GetOptional(arguments, "image", null);
string? cpu = GetOptional(arguments, "cpu", null);
string? memory = GetOptional(arguments, "memory", null);
string? protocol = GetOptional(arguments, "protocol", null);
string? protocolVersion = GetOptional(arguments, "protocol-version", null);
var environment = new Dictionary<string, string>(StringComparer.Ordinal);

if (!string.IsNullOrWhiteSpace(manifestPath))
{
    string manifestText = ReadManifestWithSubstitutions(manifestPath, GetMulti(arguments, "set"));
    ApplyManifest(manifestText, manifestPath, ref image, ref protocol, ref protocolVersion, environment);
}

// --env NAME=VALUE flags extend/override manifest environment variables.
foreach (string pair in GetMulti(arguments, "env"))
{
    string[] parts = pair.Split('=', 2, StringSplitOptions.TrimEntries);
    if (parts.Length == 2 && parts[0].Length > 0)
    {
        environment[parts[0]] = parts[1];
    }
}

if (string.IsNullOrWhiteSpace(image))
{
    throw new InvalidOperationException("A container image is required (use --image or a manifest with template.image).");
}

cpu ??= "1";
memory ??= "2Gi";
protocol ??= "responses";
protocolVersion ??= "1.0.0";

var protocolVersions = new[] { new ProtocolVersionRecord(ParseProtocol(protocol), protocolVersion) };
HostedAgentDefinition definition = ProjectsAgentDefinition.CreateHostedAgentDefinition(protocolVersions, cpu, memory);
definition.ContainerConfiguration = new ContainerConfiguration(image);
foreach (KeyValuePair<string, string> kv in environment)
{
    definition.EnvironmentVariables[kv.Key] = kv.Value;
}

Console.WriteLine($"Registering hosted agent '{agentName}'");
Console.WriteLine($"  Project  : {projectEndpoint}");
Console.WriteLine($"  Image    : {image}");
Console.WriteLine($"  Resources: cpu={cpu}, memory={memory}");
Console.WriteLine($"  Protocol : {protocol} {protocolVersion}");

var client = new AgentAdministrationClient(new Uri(projectEndpoint), new DefaultAzureCredential());
var options = new ProjectsAgentVersionCreationOptions(definition);
ProjectsAgentVersion version = client.CreateAgentVersion(agentName, options, foundryFeatures: foundryFeatures).Value;

Console.WriteLine($"Created hosted agent '{version.Name}' version {version.Version}.");
Console.WriteLine("Next: start the container with `az cognitiveservices agent start` (see the Lab 4 appendix).");

static ProjectsAgentProtocol ParseProtocol(string value) => value.Trim().ToLowerInvariant() switch
{
    "invocations" => ProjectsAgentProtocol.Invocations,
    _ => ProjectsAgentProtocol.Responses,
};

static string ReadManifestWithSubstitutions(string path, IReadOnlyList<string> setValues)
{
    string text = File.ReadAllText(path);
    foreach (string replacement in setValues)
    {
        string[] parts = replacement.Split('=', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || parts[0].Length == 0)
        {
            throw new ArgumentException($"Invalid --set value '{replacement}'. Use NAME=VALUE.");
        }

        text = text
            .Replace($"${{{parts[0]}}}", parts[1], StringComparison.Ordinal)
            .Replace($"{{{{{parts[0]}}}}}", parts[1], StringComparison.Ordinal);
    }

    return text;
}

static void ApplyManifest(
    string manifestText,
    string manifestPath,
    ref string? image,
    ref string? protocol,
    ref string? protocolVersion,
    IDictionary<string, string> environment)
{
    var deserializer = new DeserializerBuilder().Build();
    if (deserializer.Deserialize<object>(new StringReader(manifestText)) is not IDictionary<object, object> root)
    {
        throw new InvalidOperationException($"Manifest '{manifestPath}' is not a valid YAML mapping.");
    }

    if (!root.TryGetValue("template", out object? templateObj) || templateObj is not IDictionary<object, object> template)
    {
        throw new InvalidOperationException($"Manifest '{manifestPath}' is missing a 'template' block.");
    }

    if (image is null && template.TryGetValue("image", out object? imageObj) && imageObj is string imageValue && imageValue.Length > 0)
    {
        image = imageValue;
    }

    if (template.TryGetValue("protocols", out object? protocolsObj)
        && protocolsObj is IList<object> protocols
        && protocols.Count > 0
        && protocols[0] is IDictionary<object, object> firstProtocol)
    {
        if (protocol is null && firstProtocol.TryGetValue("protocol", out object? pObj) && pObj is string pValue)
        {
            protocol = pValue;
        }

        if (protocolVersion is null && firstProtocol.TryGetValue("version", out object? vObj) && vObj is string vValue)
        {
            protocolVersion = vValue;
        }
    }

    if (template.TryGetValue("environment_variables", out object? envObj) && envObj is IList<object> envList)
    {
        foreach (object entry in envList)
        {
            if (entry is IDictionary<object, object> pair
                && pair.TryGetValue("name", out object? nameObj) && nameObj is string name && name.Length > 0
                && pair.TryGetValue("value", out object? valueObj) && valueObj is string value
                && !environment.ContainsKey(name))
            {
                environment[name] = value;
            }
        }
    }
}

static Dictionary<string, List<string>> ParseArguments(string[] args)
{
    var parsed = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

    for (int index = 0; index < args.Length; index++)
    {
        string current = args[index];
        if (!current.StartsWith("--", StringComparison.Ordinal))
        {
            continue;
        }

        string key = current[2..];
        string value = index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal)
            ? args[++index]
            : "true";

        if (!parsed.TryGetValue(key, out List<string>? values))
        {
            values = [];
            parsed[key] = values;
        }

        values.Add(value);
    }

    return parsed;
}

static string GetRequired(Dictionary<string, List<string>> args, string argName, string envName)
{
    string? value = GetOptional(args, argName, envName);
    return !string.IsNullOrWhiteSpace(value)
        ? value
        : throw new InvalidOperationException($"Missing required value '--{argName}' or environment variable '{envName}'.");
}

static string? GetOptional(Dictionary<string, List<string>> args, string argName, string? envName)
{
    if (args.TryGetValue(argName, out List<string>? values) && values.Count > 0)
    {
        return values[^1];
    }

    return envName is null ? null : Environment.GetEnvironmentVariable(envName);
}

static IReadOnlyList<string> GetMulti(Dictionary<string, List<string>> args, string argName)
{
    return args.TryGetValue(argName, out List<string>? values) ? values : [];
}
