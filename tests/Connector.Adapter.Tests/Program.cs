using System.Text.Json.Nodes;
using Connector.Features.Discovery;
using Connector.Features.Discovery.GetOperationSchema;
using Connector.Host;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Connector.Features.Execution.InvokeOperation;
using System.Net;
using System.Text;

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
var provider = new ApimServiceProvider();
var settings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
var manifest = JObject.Parse(JsonConvert.SerializeObject(provider.Manifest("invokeApi"), settings));
Require(manifest["inputs"]?["properties"]?["apiName"]?["x-ms-dynamic-list"]?["dynamicState"]?["operationId"]?.Value<string>() == "listApis", "API dynamic handler missing");
Require(manifest["inputs"]?["properties"]?["apiVersion"]?["x-ms-dynamic-list"]?["parameters"]?["apiName"]?["parameterReference"]?.Value<string>() == "apiName", "Version dependency missing");
Require(manifest["outputs"]?["properties"]?["body"]?["x-ms-dynamic-properties"]?["dynamicState"]?["extension"]?["operationId"]?.Value<string>() == "outputSchema", "Dynamic output handler missing");
Require(provider.GetOperations(true).Count() == 6, "Internal dynamic handlers not registered");
var binding = new OperationBinding("orders-v1", "v1", "GetOrder", "GET", "poc-orders", "/orders/{orderId}", new VersionRouting("Segment", null, null), one.Inputs, one.Outputs);
using (var handler = new GatewayHandler(async request =>
{
    Require(request.RequestUri!.Host == "gateway.example.nl", "Runtime called management or an unexpected host");
    Require(request.RequestUri.AbsolutePath == "/gateway/poc-orders/v1/orders/a%20b", "Version/path encoding wrong");
    Require(request.Headers.Authorization?.Parameter == "business-token", "Business token missing");
    await Task.CompletedTask;
}, HttpStatusCode.OK, "{\"id\":\"a b\",\"customerName\":\"Example\"}"))
using (var http = new HttpClient(handler))
{
    var result = await new ApiExecutor(http).InvokeAsync(binding, new JsonObject { ["pathParameters"] = new JsonObject { ["orderId"] = "a b" } }, new Uri("https://gateway.example.nl/gateway"), "business-token", null);
    Require(result.Status == HttpStatusCode.OK && result.Body?["id"]?.GetValue<string>() == "a b", "GET response lost");
}
var postBinding = binding with { Method = "POST", OperationId = "CreateOrder", OperationPath = "/orders", Inputs = post.Inputs, Outputs = post.Outputs };
using (var handler = new GatewayHandler(async request =>
{
    Require(request.Method == HttpMethod.Post, "POST method lost");
    var content = JsonNode.Parse(await request.Content!.ReadAsStringAsync())!;
    Require(content["customerName"]?.GetValue<string>() == "Example", "POST body wrong");
}, HttpStatusCode.Created, "{\"id\":\"new\",\"customerName\":\"Example\"}"))
using (var http = new HttpClient(handler))
{
    var result = await new ApiExecutor(http).InvokeAsync(postBinding, new JsonObject { ["body"] = new JsonObject { ["customerName"] = "Example" } }, new Uri("https://gateway.example.nl/"), "business-token", null);
    Require(result.Status == HttpStatusCode.Created, "201 status lost");
}
using (var http = new HttpClient(new GatewayHandler(_ => throw new Exception("Validation should prevent HTTP"), HttpStatusCode.OK, "{}")))
{
    await FailsAsync("RequiredValueMissing", () => new ApiExecutor(http).InvokeAsync(postBinding, new JsonObject { ["body"] = new JsonObject() }, new Uri("https://gateway.example.nl/"), "business-token", null));
}
Console.WriteLine("PASS: schemas, pinned SDK metadata and GET/POST runtime without ARM requests");

static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
static void Fails(string expected, Action action)
{
    try { action(); throw new Exception("Expected " + expected); }
    catch (DiscoveryException error) { Require(error.Code == expected, "Wrong diagnostic " + error.Code); }
}
static async Task FailsAsync(string expected, Func<Task<ApiResult>> action)
{
    try { await action(); throw new Exception("Expected " + expected); }
    catch (DiscoveryException error) { Require(error.Code == expected, "Wrong diagnostic " + error.Code); }
}

sealed class GatewayHandler(Func<HttpRequestMessage, Task> inspect, HttpStatusCode status, string body) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await inspect(request);
        return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
