using System.Text.Json;
using System.Text.Json.Nodes;
using Connector.Features.Discovery;
using Connector.Features.Execution.InvokeOperation;

namespace Connector.Features.Deployment.ValidateBinding;

public sealed record SavedBinding(string ActionPath, OperationBinding Binding);

public static class WorkflowBindings
{
    public static IReadOnlyList<SavedBinding> Read(JsonObject workflow)
    {
        var result = new List<SavedBinding>();
        Walk(workflow, "workflow", result);
        if (result.Count == 0) throw new DiscoveryException("NoApimBindingsFound");
        return result;
    }

    private static void Walk(JsonNode? node, string path, List<SavedBinding> result)
    {
        if (node is JsonObject obj)
        {
            if (obj["type"]?.GetValue<string>() == "ServiceProvider" &&
                obj["inputs"]?["serviceProviderConfiguration"]?["serviceProviderId"]?.GetValue<string>() == "/serviceProviders/apimconnector")
            {
                if (obj["inputs"]!["serviceProviderConfiguration"]!["operationId"]?.GetValue<string>() != "invokeApi")
                    throw new DiscoveryException("UnsupportedApimWorkflowOperation");
                var parameters = obj["inputs"]?["parameters"] as JsonObject ?? throw new DiscoveryException("BindingSnapshotMissing");
                var snapshot = parameters["payload"]?["_binding"]?.GetValue<string>() ?? throw new DiscoveryException("BindingSnapshotMissing");
                OperationBinding binding;
                try { binding = JsonSerializer.Deserialize<OperationBinding>(snapshot) ?? throw new DiscoveryException("BindingSnapshotInvalid"); }
                catch (JsonException) { throw new DiscoveryException("BindingSnapshotInvalid"); }
                if (binding.Inputs is null || binding.Outputs is null || string.IsNullOrEmpty(binding.ApiSelector)) throw new DiscoveryException("BindingSnapshotInvalid");
                if (binding.ApiSelector != parameters["apiName"]?.GetValue<string>() || binding.ApiName != parameters["apiVersion"]?.GetValue<string>() ||
                    binding.OperationId != parameters["operationId"]?.GetValue<string>()) throw new DiscoveryException("BindingSelectionChanged");
                result.Add(new SavedBinding(path, binding));
                return;
            }
            foreach (var entry in obj) Walk(entry.Value, path + "/" + entry.Key, result);
        }
        else if (node is JsonArray array)
            for (var i = 0; i < array.Count; i++) Walk(array[i], path + "/" + i, result);
    }
}
