using System.Text.Json.Nodes;
using Connector.Features.Discovery;
using Connector.Features.Discovery.GetOperationSchema;

var v1 = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "v1.openapi.json")))!.AsObject();
var v2 = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "v2.openapi.json")))!.AsObject();
var one = OperationSchema.Read(v1, "GET", "/orders/{orderId}");
Require(one.Inputs["properties"]?["pathParameters"]?["required"]?.AsArray().Any(x => x!.GetValue<string>() == "orderId") == true, "Required path parameter lost");
Require(one.Outputs["properties"]?["customerName"]?["type"]?.GetValue<string>() == "string", "Referenced output not resolved");
Require(one.Outputs["properties"]?["status"] is null, "v2 field leaked into v1");
var two = OperationSchema.Read(v2, "GET", "/orders/{orderId}");
Require(two.Outputs["properties"]?["status"]?["enum"]?.AsArray().Count == 2, "v2 status lost");
var post = OperationSchema.Read(v1, "POST", "/orders");
Require(post.Inputs["properties"]?["body"]?["required"]?.AsArray().Any(x => x!.GetValue<string>() == "customerName") == true, "Required POST body lost");
var external = v1.DeepClone().AsObject();
external["components"]!["schemas"]!["Order"] = new JsonObject { ["$ref"] = "https://external.example/order.json" };
Fails("ExternalSchemaReferenceUnsupported", () => OperationSchema.Read(external, "GET", "/orders/{orderId}"));
var recursive = v1.DeepClone().AsObject();
recursive["components"]!["schemas"]!["Order"]!["properties"]!["parent"] = new JsonObject { ["$ref"] = "#/components/schemas/Order" };
Fails("RecursiveSchemaUnsupported", () => OperationSchema.Read(recursive, "GET", "/orders/{orderId}"));
var ambiguous = v1.DeepClone().AsObject();
ambiguous["paths"]!["/orders/{orderId}"]!["get"]!["responses"]!["201"] = new JsonObject
{
    ["description"] = "Different success shape",
    ["content"] = new JsonObject { ["application/json"] = new JsonObject { ["schema"] = new JsonObject { ["type"] = "object", ["properties"] = new JsonObject { ["other"] = new JsonObject { ["type"] = "integer" } } } } }
};
Fails("AmbiguousSuccessSchemas", () => OperationSchema.Read(ambiguous, "GET", "/orders/{orderId}"));
Console.WriteLine("PASS: operation schemas preserve required fields, version shapes, and reject unsafe/ambiguous contracts");

static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
static void Fails(string expected, Action action)
{
    try { action(); throw new Exception("Expected " + expected); }
    catch (DiscoveryException error) { Require(error.Code == expected, "Wrong diagnostic " + error.Code); }
}
