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
        var definition = workflow["definition"] as JsonObject ?? workflow;
        WalkActions(definition["actions"] as JsonObject, "workflow/actions", result);
        if (result.Count == 0) throw new DiscoveryException("NoApimBindingsFound");
        return result;
    }

    private static void WalkActions(JsonObject? actions, string path, List<SavedBinding> result)
    {
        if (actions is null) return;
        foreach (var action in actions)
        {
            if (action.Value is not JsonObject obj) throw new DiscoveryException("WorkflowActionInvalid");
            var actionPath = path + "/" + action.Key;
            if (obj["type"] is JsonValue type && type.TryGetValue<string>(out var actionType) && actionType == "ServiceProvider" &&
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
                result.Add(new SavedBinding(actionPath, binding));
            }
            WalkActions(obj["actions"] as JsonObject, actionPath + "/actions", result);
            WalkActions(obj["else"]?["actions"] as JsonObject, actionPath + "/else/actions", result);
            WalkActions(obj["default"]?["actions"] as JsonObject, actionPath + "/default/actions", result);
            if (obj["cases"] is JsonObject cases)
                foreach (var branch in cases) WalkActions(branch.Value?["actions"] as JsonObject, actionPath + "/cases/" + branch.Key + "/actions", result);
        }
    }
}
