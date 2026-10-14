using Connector.Features.Discovery;
using System.Text.Json;
using System.Text.Json.Nodes;
using Connector.Features.Deployment.ValidateBinding;

if (args.Length < 3 || !Guid.TryParse(args[0], out var subscription))
{
    Console.Error.WriteLine("Usage: <subscription-guid> <resource-group> <apim-name> [apis|versions|operations|export|validate-workflow] [api-name|workflow-path]");
    return 2;
}

try
{
    using var transport = new HttpClientHandler { AllowAutoRedirect = false };
    using var http = new HttpClient(transport) { Timeout = TimeSpan.FromSeconds(60) };
    var discovery = new ApimDiscoveryClient(http, subscription, args[1], args[2], _ =>
        Task.FromResult(Environment.GetEnvironmentVariable("APIM_DEVELOPER_ARM_TOKEN") ?? ""));
    var command = args.Length > 3 ? args[3] : "apis";
    if (command == "validate-workflow" && args.Length == 5)
    {
        var workflow = JsonNode.Parse(await File.ReadAllTextAsync(args[4]))?.AsObject() ?? throw new DiscoveryException("WorkflowInvalid");
        var bindings = WorkflowBindings.Read(workflow);
        foreach (var saved in bindings)
        {
            await BindingValidator.ValidateTargetAsync(saved.Binding, discovery);
            Console.WriteLine(JsonSerializer.Serialize(new { action = saved.ActionPath, compatibility = "Compatible" }));
        }
        return 0;
    }
    object result = command switch
    {
        "apis" => await discovery.ListApisAsync(),
        "versions" when args.Length == 5 => await discovery.ListVersionsAsync(args[4]),
        "operations" when args.Length == 5 => await discovery.ListOperationsAsync(args[4]),
        "export" when args.Length == 5 => await discovery.ExportContractAsync(args[4]),
        _ => throw new ArgumentException("Invalid command or missing API name.")
    };
    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
catch (DiscoveryException error) { Console.Error.WriteLine(error.Message); return 1; }
catch (OperationCanceledException) { Console.Error.WriteLine("DiscoveryCancelledOrTimedOut"); return 1; }
catch (Exception) { Console.Error.WriteLine("DiscoveryFailed: check resource arguments and the response format."); return 1; }
