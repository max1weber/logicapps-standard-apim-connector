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
