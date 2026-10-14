using System.Text.Json.Nodes;

namespace Connector.Features.Discovery.GetOperationSchema;

public sealed record OperationSchemas(JsonObject Inputs, JsonObject Outputs);

/// <summary>Normalises one REST/JSON operation. Unsupported constructs fail explicitly.</summary>
public static class OperationSchema
{
    public static void RemoveVersionParameter(JsonObject inputs, VersionRouting? routing)
    {
        var group = routing?.Scheme switch { "Query" => "queryParameters", "Header" => "headers", _ => null };
        var name = routing?.Scheme == "Query" ? routing.QueryName : routing?.HeaderName;
        if (group is null || name is null || inputs["properties"]?[group] is not JsonObject obj) return;
        obj["properties"]?.AsObject().Remove(name);
        if (obj["required"] is JsonArray required)
            for (var i = required.Count - 1; i >= 0; i--) if (required[i]?.GetValue<string>() == name) required.RemoveAt(i);
        if (obj["properties"]?.AsObject().Count == 0) inputs["properties"]!.AsObject().Remove(group);
        if ((obj["required"] as JsonArray)?.Count == 0 && inputs["required"] is JsonArray groups)
            for (var i = groups.Count - 1; i >= 0; i--) if (groups[i]?.GetValue<string>() == group) groups.RemoveAt(i);
    }

    public static OperationSchemas Read(JsonObject contract, string method, string urlTemplate)
    {
        var path = urlTemplate.Split('?')[0];
        var pathItem = contract["paths"]?[path] as JsonObject ?? throw new DiscoveryException("OperationPathNotFound");
        var operation = pathItem[method.ToLowerInvariant()] as JsonObject ?? throw new DiscoveryException("OperationMethodNotFound");
        var properties = new JsonObject();
        var requiredGroups = new JsonArray();
        var parameters = (pathItem["parameters"] as JsonArray ?? new JsonArray()).Concat(operation["parameters"] as JsonArray ?? new JsonArray())
            .Select(p => Resolve(contract, p, new HashSet<string>(), 0)!.AsObject())
            .GroupBy(p => (p["in"]!.GetValue<string>(), p["name"]!.GetValue<string>()))
            .Select(g => g.Last());
        foreach (var group in parameters.GroupBy(p => p["in"]!.GetValue<string>()))
        {
            var name = group.Key switch { "path" => "pathParameters", "query" => "queryParameters", "header" => "headers", _ => throw new DiscoveryException("UnsupportedParameterLocation") };
            var fields = new JsonObject();
            var required = new JsonArray();
            foreach (var parameter in group)
            {
                var key = parameter["name"]!.GetValue<string>();
                if (parameter["style"] is not null || parameter["explode"] is not null)
                    throw new DiscoveryException("UnsupportedParameterSerialization");
                var schema = Resolve(contract, parameter["schema"], new HashSet<string>(), 0) as JsonObject ?? throw new DiscoveryException("ParameterSchemaMissing");
                fields[key] = schema;
                if (parameter["required"]?.GetValue<bool>() == true) required.Add(key);
            }
            var obj = ObjectSchema(fields, required);
            properties[name] = obj;
            if (required.Count > 0) requiredGroups.Add(name);
        }
        if (operation["requestBody"] is JsonNode requestBody)
        {
            var body = Resolve(contract, requestBody, new HashSet<string>(), 0)!.AsObject();
            properties["body"] = JsonSchema(contract, body);
            if (body["required"]?.GetValue<bool>() == true) requiredGroups.Add("body");
        }
        var outputCandidates = new List<JsonNode>();
        foreach (var response in operation["responses"]!.AsObject())
        {
            if (response.Key.Length == 3 && response.Key.StartsWith('2'))
            {
                var resolved = Resolve(contract, response.Value, new HashSet<string>(), 0)!.AsObject();
                if (resolved["content"] is not null) outputCandidates.Add(JsonSchema(contract, resolved));
            }
        }
        if (outputCandidates.Skip(1).Any(candidate => !JsonNode.DeepEquals(candidate, outputCandidates[0])))
            throw new DiscoveryException("AmbiguousSuccessSchemas");
        return new OperationSchemas(ObjectSchema(properties, requiredGroups),
            outputCandidates.FirstOrDefault() as JsonObject ?? new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() });
    }

    private static JsonObject ObjectSchema(JsonObject properties, JsonArray required) => new()
    {
        ["type"] = "object", ["properties"] = properties, ["required"] = required, ["additionalProperties"] = false
    };

    private static JsonObject JsonSchema(JsonObject contract, JsonObject owner)
    {
        var content = owner["content"] as JsonObject ?? throw new DiscoveryException("JsonContentMissing");
        if (content.Count != 1 || content["application/json"]?["schema"] is not JsonNode schema)
            throw new DiscoveryException("UnsupportedMediaType");
        return Resolve(contract, schema, new HashSet<string>(), 0) as JsonObject ?? throw new DiscoveryException("JsonSchemaMissing");
    }

    private static JsonNode? Resolve(JsonObject root, JsonNode? node, HashSet<string> ancestors, int depth)
    {
        if (depth > 64) throw new DiscoveryException("SchemaDepthExceeded");
        if (node is JsonObject obj)
        {
            if (obj["$ref"] is JsonNode reference)
            {
                var pointer = reference.GetValue<string>();
                if (!pointer.StartsWith("#/", StringComparison.Ordinal)) throw new DiscoveryException("ExternalSchemaReferenceUnsupported");
                if (!ancestors.Add(pointer)) throw new DiscoveryException("RecursiveSchemaUnsupported");
                if (obj.Count != 1) throw new DiscoveryException("ReferenceSiblingsUnsupported");
                JsonNode? target = root;
                foreach (var part in pointer[2..].Split('/')) target = target?[part.Replace("~1", "/").Replace("~0", "~")];
                if (target is null) throw new DiscoveryException("SchemaReferenceMissing");
                var result = Resolve(root, target, ancestors, depth + 1);
                ancestors.Remove(pointer);
                return result;
            }
            if (obj.ContainsKey("allOf") || obj.ContainsKey("anyOf") || obj.ContainsKey("oneOf") || obj.ContainsKey("not") || obj.ContainsKey("nullable"))
                throw new DiscoveryException("SchemaCompositionUnsupported");
            var copy = new JsonObject();
            foreach (var entry in obj) copy[entry.Key] = Resolve(root, entry.Value, ancestors, depth + 1);
            return copy;
        }
        if (node is JsonArray array)
        {
            var copy = new JsonArray();
            foreach (var entry in array) copy.Add(Resolve(root, entry, ancestors, depth + 1));
            return copy;
        }
        return node?.DeepClone();
    }
}
