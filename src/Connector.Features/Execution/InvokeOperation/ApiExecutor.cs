using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Connector.Features.Discovery;

namespace Connector.Features.Execution.InvokeOperation;

public sealed record OperationBinding(string ApiName, string? Version, string OperationId, string Method,
    string ApiPath, string OperationPath, VersionRouting? Routing, JsonObject Inputs, JsonObject Outputs, string ApiSelector);
public sealed record ApiResult(HttpStatusCode Status, JsonNode? Body);

public sealed class ApiExecutor(HttpClient http)
{
    public async Task<ApiResult> InvokeAsync(OperationBinding binding, JsonObject payload, Uri gateway,
        string token, string? subscriptionKey, CancellationToken cancellationToken = default)
    {
        if (gateway.Scheme != "https" || gateway.UserInfo.Length != 0 || gateway.Query.Length != 0 || gateway.Fragment.Length != 0)
            throw new DiscoveryException("InvalidGateway");
        if (http.DefaultRequestHeaders.Contains("Authorization")) throw new DiscoveryException("UnsafeDefaultAuthorization");
        if (string.IsNullOrWhiteSpace(token)) throw new DiscoveryException("MissingApiToken");
        if (!new[] { "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS" }.Contains(binding.Method)) throw new DiscoveryException("UnsupportedHttpMethod");
        Validate(binding.Inputs, payload);
        var path = Regex.Replace(binding.OperationPath.Split('?')[0], "\\{([^{}]+)\\}", match =>
        {
            var value = Scalar(payload["pathParameters"]?[match.Groups[1].Value]);
            if (value is "." or "..") throw new DiscoveryException("UnsafePathParameter");
            return Uri.EscapeDataString(value);
        });
        var prefix = gateway.AbsolutePath.TrimEnd('/') + "/" + binding.ApiPath.Trim('/');
        if (binding.Routing?.Scheme == "Segment") prefix += "/" + Uri.EscapeDataString(binding.Version ?? throw new DiscoveryException("VersionMissing"));
        if ((prefix + "/" + path).Split('/').Any(segment => segment is "." or "..") || path.Contains('\\')) throw new DiscoveryException("UnsafeOperationPath");
        var query = new Dictionary<string, string>(StringComparer.Ordinal);
        if (payload["queryParameters"] is JsonObject queryValues) foreach (var value in queryValues) query.Add(value.Key, Scalar(value.Value));
        if (binding.OperationPath.Split('?', 2) is { Length: 2 } template)
        {
            foreach (var pair in template[1].Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split('=', 2);
                var key = Uri.UnescapeDataString(parts[0]);
                if (key.Contains('{') || key.Contains('}')) throw new DiscoveryException("QueryTemplateKeyUnsupported");
                var literal = Uri.UnescapeDataString(parts.Length > 1 ? parts[1] : "");
                var bound = Regex.Replace(literal, "\\{([^{}]+)\\}", match =>
                {
                    if (!query.TryGetValue(match.Groups[1].Value, out var parameter)) throw new DiscoveryException("QueryTemplateParameterMissing");
                    return parameter;
                });
                if (!literal.Contains('{') && query.TryGetValue(key, out var supplied) && supplied != bound) throw new DiscoveryException("QueryTemplateOverride");
                query[key] = bound;
            }
        }
        if (binding.Routing?.Scheme == "Query") AddVersion(query, binding.Routing.QueryName, binding.Version);
        var url = gateway.GetLeftPart(UriPartial.Authority) + prefix.TrimEnd('/') + "/" + path.TrimStart('/');
        if (query.Count > 0) url += "?" + string.Join("&", query.Select(pair => Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value)));
        using var request = new HttpRequestMessage(new HttpMethod(binding.Method), new Uri(url));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (!string.IsNullOrEmpty(subscriptionKey)) request.Headers.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
        if (payload["headers"] is JsonObject headers)
        {
            foreach (var header in headers)
            {
                if (new[] { "Authorization", "Host", "Content-Type", "Content-Length", "Ocp-Apim-Subscription-Key" }.Contains(header.Key, StringComparer.OrdinalIgnoreCase) ||
                    string.Equals(header.Key, binding.Routing?.HeaderName, StringComparison.OrdinalIgnoreCase)) throw new DiscoveryException("ProtectedHeaderOverride");
                request.Headers.Add(header.Key, Scalar(header.Value));
            }
        }
        if (binding.Routing?.Scheme == "Header") request.Headers.Add(binding.Routing.HeaderName ?? throw new DiscoveryException("VersionHeaderMissing"), binding.Version);
        if (payload["body"] is JsonNode body) request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if ((int)response.StatusCode >= 300) return new ApiResult(response.StatusCode, new JsonObject { ["error"] = "ApiRequestFailed", ["statusCode"] = (int)response.StatusCode });
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var bytes = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(bytes, cancellationToken)) > 0)
        {
            if (buffer.Length + count > 8 * 1024 * 1024) throw new DiscoveryException("ResponseTooLarge");
            await buffer.WriteAsync(bytes.AsMemory(0, count), cancellationToken);
        }
        if (buffer.Length == 0) return new ApiResult(response.StatusCode, null);
        try { return new ApiResult(response.StatusCode, JsonNode.Parse(buffer.ToArray())); }
        catch (JsonException) { throw new DiscoveryException("NonJsonResponse"); }
    }

    private static void AddVersion(Dictionary<string, string> query, string? name, string? version)
    {
        if (string.IsNullOrEmpty(name) || version is null) throw new DiscoveryException("VersionQueryMissing");
        if (!query.TryAdd(name, version)) throw new DiscoveryException("VersionOverride");
    }

    private static string Scalar(JsonNode? value)
    {
        if (value is not JsonValue scalar) throw new DiscoveryException("ScalarParameterRequired");
        return scalar.TryGetValue<string>(out var text) ? text : scalar.ToJsonString();
    }

    private static void Validate(JsonObject schema, JsonNode? value)
    {
        if (value is null) throw new DiscoveryException("RequiredValueMissing");
        var type = schema["type"]?.GetValue<string>();
        if (type == "object")
        {
            if (value is not JsonObject obj) throw new DiscoveryException("ObjectRequired");
            foreach (var name in schema["required"] as JsonArray ?? new JsonArray())
                if (obj[name!.GetValue<string>()] is null) throw new DiscoveryException("RequiredValueMissing");
            var fields = schema["properties"] as JsonObject ?? new JsonObject();
            foreach (var entry in obj)
            {
                if (fields[entry.Key] is JsonObject field) Validate(field, entry.Value);
                else if (schema["additionalProperties"] is JsonValue allowed && allowed.TryGetValue<bool>(out var flag) && !flag)
                    throw new DiscoveryException("UnexpectedPayloadField");
            }
        }
        else if (type == "array")
        {
            if (value is not JsonArray array || schema["items"] is not JsonObject itemSchema) throw new DiscoveryException("ArrayRequired");
            foreach (var item in array) Validate(itemSchema, item);
        }
        else
        {
            using var doc = JsonDocument.Parse(value.ToJsonString());
            var kind = doc.RootElement.ValueKind;
            if (type == "string" && kind != JsonValueKind.String || type == "boolean" && kind is not (JsonValueKind.True or JsonValueKind.False) ||
                type is "number" or "integer" && kind != JsonValueKind.Number || type == "integer" && !doc.RootElement.TryGetInt64(out _))
                throw new DiscoveryException("PayloadTypeMismatch");
        }
        if (schema["enum"] is JsonArray options && !options.Any(option => JsonNode.DeepEquals(option, value))) throw new DiscoveryException("InvalidEnumValue");
    }
}
