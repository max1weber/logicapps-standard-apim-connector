using Connector.Features.Discovery;
using System.Net;
using System.Text;

var scope = "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/test/providers/Microsoft.ApiManagement/service/apim";
var passed = 0;

await Check("Inventory includes all pages and excludes non-current revisions", async () =>
{
    using var handler = new SequenceHandler(
        (request => Require(request.Headers.Authorization?.Parameter == "developer", "Missing developer token"),
            "{\"value\":[{\"id\":\"api1\",\"name\":\"orders-v1\",\"properties\":{\"path\":\"orders\",\"apiVersion\":\"v1\",\"apiVersionSetId\":\"set1\",\"isCurrent\":true}}],\"nextLink\":\"https://management.azure.com" + scope + "/apis?api-version=2024-05-01&$skip=1\"}"),
        (request => Require(request.RequestUri!.Query.Contains("$skip=1"), "Next page not fetched"),
            "{\"value\":[{\"id\":\"api2\",\"name\":\"orders-v2\",\"properties\":{\"apiVersion\":\"v2\",\"apiVersionSetId\":\"set1\"}},{\"id\":\"old\",\"name\":\"orders;rev=1\",\"properties\":{\"isCurrent\":false}}]}"));
    using var http = new HttpClient(handler);
    var apis = await Client(http).ListApisAsync();
    Require(apis.Select(api => api.Name).SequenceEqual(new[] { "orders-v1", "orders-v2" }), "Unexpected inventory");
    handler.AssertComplete();
});

await Check("An unversioned API remains selectable without another family's versions", async () =>
{
    using var handler = new SequenceHandler((request => { }, "{\"value\":[{\"id\":\"plain\",\"name\":\"health\",\"properties\":{}},{\"id\":\"orders\",\"name\":\"orders-v1\",\"properties\":{\"apiVersionSetId\":\"set1\",\"apiVersion\":\"v1\"}}]}"));
    using var http = new HttpClient(handler);
    var versions = await Client(http).ListVersionsAsync("health");
    Require(versions.Count == 1 && versions[0].Name == "health" && versions[0].Version is null, "Unversioned API lost");
});

await Check("Pagination cannot send developer tokens outside the configured APIM", async () =>
{
    using var handler = new SequenceHandler((request => { }, "{\"value\":[],\"nextLink\":\"https://evil.example/apis\"}"));
    using var http = new HttpClient(handler);
    await Fails("InvalidManagementScope", async () => { await Client(http).ListApisAsync(); });
    handler.AssertComplete();
});

await Check("Forbidden discovery returns a redacted status without the server body", async () =>
{
    using var handler = new StatusHandler(HttpStatusCode.Forbidden, "sensitive-server-detail");
    using var http = new HttpClient(handler);
    try { await Client(http).ListApisAsync(); throw new Exception("Expected failure"); }
    catch (DiscoveryException error)
    {
        Require(error.Status == HttpStatusCode.Forbidden && !error.Message.Contains("sensitive"), "Unredacted failure");
    }
});

await Check("OpenAPI export uses SAS storage without forwarding the developer token", async () =>
{
    using var handler = new SequenceHandler(
        (request => Require(request.Headers.Authorization?.Parameter == "developer" && request.RequestUri!.Query.Contains("openapi%2Bjson-link"), "Export authentication or format wrong"),
            "{\"value\":{\"link\":\"https://test.blob.core.windows.net/export/order.json?sig=synthetic\"}}"),
        (request => Require(request.Headers.Authorization is null, "ARM token leaked to storage"),
            "{\"openapi\":\"3.0.1\",\"info\":{\"title\":\"Order\",\"version\":\"v1\"},\"paths\":{}}"));
    using var http = new HttpClient(handler);
    var contract = await Client(http).ExportContractAsync("orders-v1");
    Require(contract.GetProperty("info").GetProperty("version").GetString() == "v1", "Contract not returned");
    handler.AssertComplete();
});

await Check("Operations preserve HTTP method and URL template", async () =>
{
    using var handler = new SequenceHandler((request => Require(request.RequestUri!.AbsolutePath.EndsWith("/apis/orders-v1/operations"), "Wrong resource"),
        "{\"value\":[{\"name\":\"get-order\",\"properties\":{\"displayName\":\"GetOrder\",\"method\":\"GET\",\"urlTemplate\":\"/orders/{orderId}\"}}]}"));
    using var http = new HttpClient(handler);
    var operations = await Client(http).ListOperationsAsync("orders-v1");
    Require(operations.Count == 1 && operations[0].Method == "GET" && operations[0].UrlTemplate == "/orders/{orderId}", "Operation routing lost");
});

Console.WriteLine($"PASS: {passed} public discovery scenarios");
return 0;

ApimDiscoveryClient Client(HttpClient http) => new(http, Guid.Parse("00000000-0000-0000-0000-000000000001"), "test", "apim", _ => Task.FromResult("developer"));
async Task Check(string name, Func<Task> test) { await test(); passed++; Console.WriteLine("PASS " + name); }
static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
static async Task Fails(string code, Func<Task> action)
{
    try { await action(); throw new Exception("Expected " + code); }
    catch (DiscoveryException error) { Require(error.Code == code, "Unexpected error " + error.Code); }
}

sealed class SequenceHandler(params (Action<HttpRequestMessage> Inspect, string Body)[] responses) : HttpMessageHandler
{
    private int index;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (index >= responses.Length) throw new Exception("Unexpected outgoing request");
        var step = responses[index++];
        step.Inspect(request);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(step.Body, Encoding.UTF8, "application/json") });
    }
    public void AssertComplete() { if (index != responses.Length) throw new Exception("Missing outgoing request"); }
}

sealed class StatusHandler(HttpStatusCode status, string body) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
}
