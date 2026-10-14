using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Connector.Features.Discovery;

public sealed record Api(string Id, string Name, string DisplayName, string Path, string? Version, string? VersionSetId);
public sealed record ApiOperation(string Name, string DisplayName, string Method, string UrlTemplate);
public sealed record VersionRouting(string Scheme, string? HeaderName, string? QueryName);

public sealed class DiscoveryException(string code, HttpStatusCode? status = null)
    : Exception(status is null ? code : $"{code} (HTTP {(int)status})")
{
    public string Code { get; } = code;
    public HttpStatusCode? Status { get; } = status;
}

/// <summary>Read-only ARM discovery. The supplied transport must disable automatic redirects.</summary>
public sealed class ApimDiscoveryClient
{
    private const string ApiVersion = "2024-05-01";
    private const int MaximumDocumentBytes = 8 * 1024 * 1024;
    private readonly HttpClient http;
    private readonly Func<CancellationToken, Task<string>> developerToken;
    private readonly string scope;

    public ApimDiscoveryClient(HttpClient http, Guid subscription, string resourceGroup, string service,
        Func<CancellationToken, Task<string>> developerToken)
    {
        this.http = http;
        this.developerToken = developerToken;
        scope = $"/subscriptions/{subscription:D}/resourceGroups/{Segment(resourceGroup)}/providers/Microsoft.ApiManagement/service/{Segment(service)}";
    }

    public async Task<IReadOnlyList<Api>> ListApisAsync(CancellationToken cancellationToken = default)
    {
        var items = await ListAsync("apis", cancellationToken);
        return items.Where(item => !item.GetProperty("properties").TryGetProperty("isCurrent", out var current) || current.GetBoolean())
            .Select(item =>
            {
                var properties = item.GetProperty("properties");
                return new Api(item.GetProperty("id").GetString()!, item.GetProperty("name").GetString()!,
                    Text(properties, "displayName") ?? item.GetProperty("name").GetString()!,
                    Text(properties, "path") ?? "", Text(properties, "apiVersion"), Text(properties, "apiVersionSetId"));
            }).ToArray();
    }

    public async Task<IReadOnlyList<Api>> ListVersionsAsync(string selectedApiName, CancellationToken cancellationToken = default)
    {
        var apis = await ListApisAsync(cancellationToken);
        var selected = apis.SingleOrDefault(api => api.Name.Equals(selectedApiName, StringComparison.OrdinalIgnoreCase))
            ?? throw new DiscoveryException("ApiNotVisible");
        return apis.Where(api => selected.VersionSetId is null
            ? api.Id.Equals(selected.Id, StringComparison.OrdinalIgnoreCase)
            : string.Equals(api.VersionSetId, selected.VersionSetId, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    public async Task<IReadOnlyList<ApiOperation>> ListOperationsAsync(string apiName, CancellationToken cancellationToken = default)
    {
        var items = await ListAsync($"apis/{Segment(apiName)}/operations", cancellationToken);
        return items.Select(item =>
        {
            var properties = item.GetProperty("properties");
            return new ApiOperation(item.GetProperty("name").GetString()!, Text(properties, "displayName") ?? "",
                Text(properties, "method") ?? "", Text(properties, "urlTemplate") ?? "");
        }).ToArray();
    }

    public async Task<VersionRouting> GetVersionRoutingAsync(string versionSetId, CancellationToken cancellationToken = default)
    {
        var uri = ManagementUri(new Uri($"https://management.azure.com{versionSetId}?api-version={ApiVersion}"));
        if (!uri.AbsolutePath.StartsWith(scope + "/apiVersionSets/", StringComparison.OrdinalIgnoreCase))
            throw new DiscoveryException("InvalidVersionSetScope");
        using var document = await ReadAsync(uri, authenticated: true, cancellationToken);
        var properties = document.RootElement.GetProperty("properties");
        return new VersionRouting(Text(properties, "versioningScheme") ?? "", Text(properties, "versionHeaderName"), Text(properties, "versionQueryName"));
    }

    public async Task<JsonElement> ExportContractAsync(string apiName, CancellationToken cancellationToken = default)
    {
        var uri = new Uri($"https://management.azure.com{scope}/apis/{Segment(apiName)}?format=openapi%2Bjson-link&export=true&api-version={ApiVersion}");
        using var export = await ReadAsync(uri, authenticated: true, cancellationToken);
        var link = export.RootElement.GetProperty("value").GetProperty("link").GetString();
        if (!Uri.TryCreate(link, UriKind.Absolute, out var blob) || blob.Scheme != "https" || blob.Port != 443 ||
            blob.UserInfo.Length != 0 || blob.Fragment.Length != 0 || !blob.Host.EndsWith(".blob.core.windows.net", StringComparison.OrdinalIgnoreCase))
            throw new DiscoveryException("InvalidExportLink");
        // SAS authorises this request: never forward the ARM token to storage.
        using var contract = await ReadAsync(blob, authenticated: false, cancellationToken);
        if (!contract.RootElement.TryGetProperty("openapi", out _)) throw new DiscoveryException("UnsupportedContractFormat");
        return contract.RootElement.Clone();
    }

    private async Task<List<JsonElement>> ListAsync(string relativePath, CancellationToken cancellationToken)
    {
        Uri? next = new($"https://management.azure.com{scope}/{relativePath}?api-version={ApiVersion}");
        var expectedPath = next.AbsolutePath;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<JsonElement>();
        while (next is not null)
        {
            next = ManagementUri(next);
            if (!next.AbsolutePath.Equals(expectedPath, StringComparison.OrdinalIgnoreCase)) throw new DiscoveryException("InvalidPaginationScope");
            if (!seen.Add(next.AbsoluteUri)) throw new DiscoveryException("PaginationLoop");
            using var document = await ReadAsync(next, authenticated: true, cancellationToken);
            foreach (var item in document.RootElement.GetProperty("value").EnumerateArray()) result.Add(item.Clone());
            var link = Text(document.RootElement, "nextLink");
            next = string.IsNullOrEmpty(link) ? null : new Uri(next, link);
        }
        return result;
    }

    private async Task<JsonDocument> ReadAsync(Uri uri, bool authenticated, CancellationToken cancellationToken)
    {
        if (http.DefaultRequestHeaders.Contains("Authorization"))
            throw new DiscoveryException("UnsafeDefaultAuthorization");
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (authenticated)
        {
            var token = await developerToken(cancellationToken);
            if (string.IsNullOrWhiteSpace(token)) throw new DiscoveryException("MissingDeveloperToken");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        HttpResponseMessage response;
        try { response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken); }
        catch (HttpRequestException) { throw new DiscoveryException("DiscoveryTransportFailed"); }
        using (response)
        {
            if (!response.IsSuccessStatusCode) throw new DiscoveryException("DiscoveryRequestFailed", response.StatusCode);
            using var buffer = new MemoryStream();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var bytes = new byte[8192];
            int count;
            while ((count = await stream.ReadAsync(bytes, cancellationToken)) > 0)
            {
                if (buffer.Length + count > MaximumDocumentBytes) throw new DiscoveryException("DocumentTooLarge");
                await buffer.WriteAsync(bytes.AsMemory(0, count), cancellationToken);
            }
            try { return JsonDocument.Parse(buffer.ToArray()); }
            catch (JsonException) { throw new DiscoveryException("InvalidDiscoveryJson"); }
        }
    }

    private Uri ManagementUri(Uri uri)
    {
        if (uri.Scheme != "https" || uri.Port != 443 || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0 ||
            !uri.Host.Equals("management.azure.com", StringComparison.OrdinalIgnoreCase) ||
            !uri.AbsolutePath.StartsWith(scope + "/", StringComparison.OrdinalIgnoreCase)) throw new DiscoveryException("InvalidManagementScope");
        return uri;
    }

    private static string Segment(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value is "." or ".." || value.Any(c => c is '/' or '\\' || char.IsControl(c)))
            throw new ArgumentException("Invalid resource name.");
        return Uri.EscapeDataString(value);
    }

    private static string? Text(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
