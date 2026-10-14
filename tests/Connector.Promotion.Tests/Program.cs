using System.Text.Json.Nodes;
using Connector.Features.Deployment.ValidateBinding;
using Connector.Features.Execution.InvokeOperation;

var inputs = JsonNode.Parse("{\"type\":\"object\",\"properties\":{\"body\":{\"type\":\"string\",\"description\":\"DEV documentation\"}}}")!.AsObject();
var outputs = JsonNode.Parse("{\"type\":\"object\",\"properties\":{\"id\":{\"type\":\"string\"}},\"required\":[\"id\"]}")!.AsObject();
var source = new OperationBinding("orders-dev", "v1", "create", "POST", "orders", "/", null, inputs, outputs, "versionset:orders");
var targetInputs = inputs.DeepClone().AsObject();
targetInputs["properties"]!["body"]!["description"] = "TST documentation";
BindingValidator.ValidateContract(source, source with { ApiName = "orders-tst", Inputs = targetInputs });
Console.WriteLine("PASS: target annotations and resource name do not rewrite the source binding");
var changedInputs = inputs.DeepClone().AsObject();
changedInputs["properties"]!["body"]!["type"] = "integer";
Fails("TargetInputContractChanged", () => BindingValidator.ValidateContract(source, source with { Inputs = changedInputs }));
var changedOutputs = outputs.DeepClone().AsObject();
changedOutputs["properties"]!["id"]!["type"] = "integer";
Fails("TargetOutputContractChanged", () => BindingValidator.ValidateContract(source, source with { Outputs = changedOutputs }));
Fails("TargetRoutingChanged", () => BindingValidator.ValidateContract(source, source with { Method = "GET" }));
Fails("TargetRoutingChanged", () => BindingValidator.ValidateContract(source, source with { ApiPath = "different" }));
Fails("TargetSelectionChanged", () => BindingValidator.ValidateContract(source, source with { Version = "v2" }));
var fieldNamedDescription = outputs.DeepClone().AsObject();
fieldNamedDescription["properties"]!["description"] = new JsonObject { ["type"] = "string" };
Fails("TargetOutputContractChanged", () => BindingValidator.ValidateContract(source, source with { Outputs = fieldNamedDescription }));
var orderedOutputs = outputs.DeepClone().AsObject();
orderedOutputs["properties"]!["name"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("a", "b") };
orderedOutputs["required"] = new JsonArray("id", "name");
var reorderedOutputs = orderedOutputs.DeepClone().AsObject();
reorderedOutputs["required"] = new JsonArray("name", "id");
reorderedOutputs["properties"]!["name"]!["enum"] = new JsonArray("b", "a");
BindingValidator.ValidateContract(source with { Outputs = orderedOutputs }, source with { Outputs = reorderedOutputs });
var workflow = new JsonObject
{
    ["definition"] = new JsonObject { ["actions"] = new JsonObject { ["Create_order"] = new JsonObject
    {
        ["type"] = "ServiceProvider", ["inputs"] = new JsonObject
        {
            ["serviceProviderConfiguration"] = new JsonObject { ["serviceProviderId"] = "/serviceProviders/apimconnector", ["operationId"] = "invokeApi", ["connectionName"] = "apim" },
            ["parameters"] = new JsonObject { ["apiName"] = source.ApiSelector, ["apiVersion"] = source.ApiName, ["operationId"] = source.OperationId,
                ["payload"] = new JsonObject { ["_binding"] = System.Text.Json.JsonSerializer.Serialize(source) } }
        }
    } } }
};
var before = workflow.ToJsonString();
var saved = WorkflowBindings.Read(workflow);
if (saved.Count != 1 || saved[0].Binding.ApiName != "orders-dev" || workflow.ToJsonString() != before) throw new Exception("Workflow changed while reading bindings");
var stale = workflow.DeepClone().AsObject();
stale["definition"]!["actions"]!["Create_order"]!["inputs"]!["parameters"]!["apiName"] = "versionset:customers";
Fails("BindingSelectionChanged", () => WorkflowBindings.Read(stale));
Fails("NoApimBindingsFound", () => WorkflowBindings.Read(new JsonObject()));
var nestedAction = workflow["definition"]!["actions"]!["Create_order"]!.DeepClone();
var mixed = workflow.DeepClone().AsObject();
var actions = mixed["definition"]!["actions"]!.AsObject();
actions["Unrelated_data"] = new JsonObject { ["type"] = "Compose", ["inputs"] = new JsonObject
{
    ["type"] = 123, ["nested"] = new JsonObject { ["type"] = new JsonObject() }, ["looks_like_action"] = nestedAction.DeepClone()
} };
actions["Loop"] = new JsonObject { ["type"] = "Foreach", ["actions"] = new JsonObject { ["Nested"] = nestedAction.DeepClone() } };
actions["Condition"] = new JsonObject { ["type"] = "If", ["actions"] = new JsonObject { ["Then"] = nestedAction.DeepClone() },
    ["else"] = new JsonObject { ["actions"] = new JsonObject { ["Otherwise"] = nestedAction.DeepClone() } } };
actions["Switch"] = new JsonObject { ["type"] = "Switch", ["cases"] = new JsonObject { ["Case1"] = new JsonObject { ["actions"] = new JsonObject { ["CaseAction"] = nestedAction.DeepClone() } } },
    ["default"] = new JsonObject { ["actions"] = new JsonObject { ["DefaultAction"] = nestedAction.DeepClone() } } };
actions["Scope"] = new JsonObject { ["type"] = "Scope", ["actions"] = new JsonObject { ["Scoped"] = nestedAction.DeepClone() } };
actions["Until"] = new JsonObject { ["type"] = "Until", ["actions"] = new JsonObject { ["Repeated"] = nestedAction.DeepClone() } };
if (WorkflowBindings.Read(mixed).Count != 8) throw new Exception("Nested real actions lost or payload data treated as actions");
var contract = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "v1.openapi.json")))!.AsObject();
var schema = Connector.Features.Discovery.GetOperationSchema.OperationSchema.Read(contract, "GET", "/orders/{orderId}");
var liveSource = source with { ApiName = "health", ApiSelector = "api:health", Version = null, OperationId = "GetOrder", Method = "GET", ApiPath = "poc-orders", OperationPath = "/orders/{orderId}", Inputs = schema.Inputs, Outputs = schema.Outputs };
foreach (var scenario in new[] { "compatible", "missing-api", "missing-version", "missing-operation", "changed-input" })
{
    using var http = new System.Net.Http.HttpClient(new TargetHandler(scenario, contract));
    var discovery = new Connector.Features.Discovery.ApimDiscoveryClient(http, Guid.Parse("00000000-0000-0000-0000-000000000001"), "test", "apim", _ => Task.FromResult("developer"));
    try
    {
        await BindingValidator.ValidateTargetAsync(liveSource, discovery);
        if (scenario != "compatible") throw new Exception("Target incompatibility accepted");
    }
    catch (Connector.Features.Discovery.DiscoveryException error)
    {
        var expected = scenario switch { "missing-api" => "TargetApiMissing", "missing-version" => "TargetApiVersionMissing", "missing-operation" => "TargetOperationMissing", "changed-input" => "TargetInputContractChanged", _ => "unexpected" };
        if (error.Code != expected) throw new Exception("Wrong target diagnostic: " + error.Code);
    }
}
Console.WriteLine("PASS: promotion rejects missing selections/routing/schema changes without changing workflow content");

static void Fails(string code, Action action)
{
    try { action(); throw new Exception("Expected " + code); }
    catch (Connector.Features.Discovery.DiscoveryException error) { if (error.Code != code) throw; }
}

sealed class TargetHandler(string scenario, JsonObject contract) : System.Net.Http.HttpMessageHandler
{
    protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        if (uri.Host is not ("management.azure.com" or "test.blob.core.windows.net")) throw new Exception("Deployment validation must not call gateway");
        if ((uri.Host == "management.azure.com") != (request.Headers.Authorization?.Parameter == "developer")) throw new Exception("Incorrect token boundary");
        string body;
        if (uri.Host == "test.blob.core.windows.net")
        {
            var exported = contract.DeepClone().AsObject();
            if (scenario == "changed-input") exported["paths"]!["/orders/{orderId}"]!["get"]!["parameters"]![0]!["schema"]!["type"] = "integer";
            body = exported.ToJsonString();
        }
        else if (uri.AbsolutePath.EndsWith("/operations"))
            body = scenario == "missing-operation" ? "{\"value\":[]}" : "{\"value\":[{\"name\":\"GetOrder\",\"properties\":{\"method\":\"GET\",\"urlTemplate\":\"/orders/{orderId}\"}}]}";
        else if (uri.Query.Contains("export=true")) body = "{\"value\":{\"link\":\"https://test.blob.core.windows.net/export/contract.json?sig=synthetic\"}}";
        else body = scenario == "missing-api" ? "{\"value\":[]}" : "{\"value\":[{\"id\":\"health\",\"name\":\"health\",\"properties\":{\"path\":\"poc-orders\"" + (scenario == "missing-version" ? ",\"apiVersion\":\"v2\"" : "") + "}}]}";
        return Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(body) });
    }
}
