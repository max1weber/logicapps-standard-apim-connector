using System.Text.Json.Nodes;
using Connector.Features.Discovery;
using Connector.Features.Discovery.GetOperationSchema;
using Connector.Features.Execution.InvokeOperation;

namespace Connector.Features.Deployment.ValidateBinding;

/// <summary>Read-only deployment gate. Never modifies saved bindings or workflow definitions.</summary>
public static class BindingValidator
{
    private static readonly HashSet<string> Annotations = ["title", "description", "examples", "example", "externalDocs"];

    public static async Task ValidateTargetAsync(OperationBinding source, ApimDiscoveryClient discovery, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Api> versions;
        try { versions = await discovery.ListVersionsAsync(source.ApiSelector, cancellationToken); }
        catch (DiscoveryException error) when (error.Code == "ApiNotVisible") { throw new DiscoveryException("TargetApiMissing"); }
        var matches = versions.Where(api => api.Version == source.Version).ToArray();
        if (matches.Length == 0) throw new DiscoveryException("TargetApiVersionMissing");
        if (matches.Length != 1) throw new DiscoveryException("TargetApiVersionAmbiguous");
        var api = matches[0];
        var operations = await discovery.ListOperationsAsync(api.Name, cancellationToken);
        var operation = operations.SingleOrDefault(candidate => candidate.Name == source.OperationId)
            ?? throw new DiscoveryException("TargetOperationMissing");
        var contract = await discovery.ExportContractAsync(api.Name, cancellationToken);
        var schemas = OperationSchema.Read(JsonNode.Parse(contract.GetRawText())!.AsObject(), operation.Method, operation.UrlTemplate);
        var routing = api.VersionSetId is null ? null : await discovery.GetVersionRoutingAsync(api.VersionSetId, cancellationToken);
        OperationSchema.RemoveVersionParameter(schemas.Inputs, routing);
        ValidateContract(source, new OperationBinding(api.Name, api.Version, operation.Name, operation.Method, api.Path,
            operation.UrlTemplate, routing, schemas.Inputs, schemas.Outputs, source.ApiSelector));
    }

    public static void ValidateContract(OperationBinding source, OperationBinding target)
    {
        if (source.ApiSelector != target.ApiSelector || source.Version != target.Version || source.OperationId != target.OperationId)
            throw new DiscoveryException("TargetSelectionChanged");
        if (source.Method != target.Method || source.ApiPath != target.ApiPath || source.OperationPath != target.OperationPath || source.Routing != target.Routing)
            throw new DiscoveryException("TargetRoutingChanged");
        if (!JsonNode.DeepEquals(NormaliseSchema(source.Inputs), NormaliseSchema(target.Inputs)))
            throw new DiscoveryException("TargetInputContractChanged");
        if (!JsonNode.DeepEquals(NormaliseSchema(source.Outputs), NormaliseSchema(target.Outputs)))
            throw new DiscoveryException("TargetOutputContractChanged");
    }

    private static JsonObject NormaliseSchema(JsonObject schema)
    {
        var result = new JsonObject();
        foreach (var entry in schema.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (Annotations.Contains(entry.Key)) continue;
            if (entry.Key is "properties" or "patternProperties" or "definitions" or "$defs" && entry.Value is JsonObject fields)
            {
                var map = new JsonObject();
                foreach (var field in fields.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                    map[field.Key] = field.Value is JsonObject child ? NormaliseSchema(child) : CanonicalValue(field.Value);
                result[entry.Key] = map;
            }
            else if (entry.Key is "items" or "additionalProperties" or "contains" or "propertyNames" && entry.Value is JsonObject child)
                result[entry.Key] = NormaliseSchema(child);
            else if (entry.Key is "required" or "enum" && entry.Value is JsonArray values)
            {
                var array = new JsonArray();
                foreach (var value in values.Select(CanonicalValue).OrderBy(value => value?.ToJsonString(), StringComparer.Ordinal)) array.Add(value);
                result[entry.Key] = array;
            }
            else result[entry.Key] = CanonicalValue(entry.Value);
        }
        return result;
    }

    private static JsonNode? CanonicalValue(JsonNode? value)
    {
        if (value is JsonObject obj)
        {
            var copy = new JsonObject();
            foreach (var entry in obj.OrderBy(pair => pair.Key, StringComparer.Ordinal)) copy[entry.Key] = CanonicalValue(entry.Value);
            return copy;
        }
        if (value is JsonArray array)
        {
            var copy = new JsonArray();
            foreach (var entry in array) copy.Add(CanonicalValue(entry));
            return copy;
        }
        return value?.DeepClone();
    }
}
