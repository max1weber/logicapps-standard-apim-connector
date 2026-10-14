using System.Net;
using System.Text.Json.Nodes;
using Connector.Features.Discovery;
using Connector.Features.Discovery.GetOperationSchema;
using Microsoft.Azure.Workflows.ServiceProviders.Abstractions;
using Microsoft.Azure.Workflows.ServiceProviders.Abstractions.Entities;
using Microsoft.WindowsAzure.ResourceStack.Common.Collections;
using Microsoft.WindowsAzure.ResourceStack.Common.Swagger.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Connector.Host;

[ServiceOperationsProvider(Id = ServiceId, Name = ServiceName)]
public sealed class ApimServiceProvider : IServiceOperationsProvider
{
    public const string ServiceName = "apimconnector";
    public const string ServiceId = "/serviceProviders/apimconnector";
    private static readonly string[] InternalOperations = ["listApis", "listVersions", "listOperations", "inputSchema", "outputSchema"];

    public ServiceOperationApi GetService() => new()
    {
        Name = ServiceName, Id = ServiceId, Type = DesignerApiType.ServiceProvider,
        Properties = new ServiceOperationApiProperties
        {
            DisplayName = "API Management", Description = "Discover API versions and operations in APIM.",
            BrandColor = 0x005A9E,
            IconUri = new Uri("https://raw.githubusercontent.com/max1weber/logicapps-standard-apim-connector/develop/assets/apim.svg"),
            Capabilities = [ApiCapability.Actions],
            ConnectionParameters = new ConnectionParameters
            {
                ConnectionString = new ConnectionStringParameters
                {
                    Type = ConnectionStringType.SecureString, ParameterSource = ConnectionParameterSource.AppConfiguration,
                    UIDefinition = new UIDefinition
                    {
                        DisplayName = "Connection profile", Description = "JSON profile from app configuration; see installation guide.",
                        Tooltip = "Use a developer-specific discovery profile.", Constraints = new Constraints { Required = "true" }
                    }
                }
            }
        }
    };

    public string GetBindingConnectionInformation(string operationId, InsensitiveDictionary<JToken> connectionParameters) =>
        throw new NotSupportedException("This connector provides actions, not Functions triggers.");

    public IEnumerable<ServiceOperation> GetOperations(bool expandManifest)
    {
        foreach (var id in new[] { "invokeApi" }.Concat(InternalOperations))
        {
            var operation = new ServiceOperation
            {
                Id = id, Name = id, Type = id,
                Properties = new ServiceOperationProperties
                {
                    Api = GetService().GetFlattenedApi(), Summary = id == "invokeApi" ? "Invoke an API operation" : id,
                    Description = id == "invokeApi" ? "Select API, version and operation." : "Internal design-time discovery.",
                    Visibility = id == "invokeApi" ? Visibility.Important : Visibility.Internal,
                    OperationType = OperationType.ServiceProvider, BrandColor = 0x005A9E,
                    IconUri = GetService().Properties.IconUri
                }
            };
            yield return expandManifest ? operation.CloneWithManifest(Manifest(id)) : operation;
        }
    }

    public async Task<ServiceOperationResponse> InvokeOperation(string operationId, InsensitiveDictionary<JToken> connectionParameters, ServiceOperationRequest request)
    {
        if (!InternalOperations.Contains(operationId))
            return new ServiceOperationResponse(new JObject { ["error"] = "RuntimeBindingNotImplemented" }, HttpStatusCode.NotImplemented);
        try
        {
            var profile = JObject.Parse(connectionParameters["connectionString"].Value<string>()!);
            var setting = profile.Value<string>("developerTokenSetting") ?? throw new DiscoveryException("DeveloperTokenSettingMissing");
            using var handler = new HttpClientHandler { AllowAutoRedirect = false };
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
            var discovery = new ApimDiscoveryClient(http, Guid.Parse(profile.Value<string>("subscriptionId")!),
                profile.Value<string>("resourceGroup")!, profile.Value<string>("serviceName")!,
                _ => Task.FromResult(Environment.GetEnvironmentVariable(setting) ?? ""));
            string Parameter(string name) => request.Parameters.TryGetValue(name, out var value) ? value.Value<string>() ?? "" : "";
            var apiName = string.IsNullOrEmpty(Parameter("apiVersion")) ? Parameter("apiName") : Parameter("apiVersion");
            var ct = request.CancellationToken;
            JToken body;
            switch (operationId)
            {
                case "listApis":
                    var apis = await discovery.ListApisAsync(ct);
                    body = JArray.FromObject(apis.GroupBy(api => api.VersionSetId ?? api.Id, StringComparer.OrdinalIgnoreCase)
                        .Select(group => new { value = group.First().Name, displayName = group.First().DisplayName }));
                    break;
                case "listVersions":
                    body = JArray.FromObject((await discovery.ListVersionsAsync(Parameter("apiName"), ct))
                        .Select(api => new { value = api.Name, displayName = api.Version ?? "Unversioned" }));
                    break;
                case "listOperations":
                    body = JArray.FromObject((await discovery.ListOperationsAsync(apiName, ct))
                        .Select(operation => new { value = operation.Name, displayName = operation.DisplayName + " (" + operation.Method + ")" }));
                    break;
                default:
                    var selected = (await discovery.ListOperationsAsync(apiName, ct)).SingleOrDefault(operation => operation.Name == Parameter("operationId"))
                        ?? throw new DiscoveryException("OperationNotVisible");
                    var contract = await discovery.ExportContractAsync(apiName, ct);
                    var schema = OperationSchema.Read(JsonNode.Parse(contract.GetRawText())!.AsObject(), selected.Method, selected.UrlTemplate);
                    body = JToken.Parse((operationId == "inputSchema" ? schema.Inputs : schema.Outputs).ToJsonString());
                    break;
            }
            return new ServiceOperationResponse(body, HttpStatusCode.OK);
        }
        catch (DiscoveryException error) { return new ServiceOperationResponse(new JObject { ["error"] = error.Code }, error.Status ?? HttpStatusCode.BadRequest); }
    }

    public ServiceOperationManifest Manifest(string operationId)
    {
        var fields = new OrdinalDictionary<SwaggerSchema>();
        if (operationId == "invokeApi")
        {
            fields.Add("apiName", Selector("API", "listApis"));
            fields.Add("apiVersion", Selector("API version", "listVersions", "apiName"));
            fields.Add("operationId", Selector("Operation", "listOperations", "apiName", "apiVersion"));
            fields.Add("payload", Schema("inputSchema", true));
        }
        var outputs = new SwaggerSchema { Type = SwaggerSchemaType.Object };
        if (operationId == "invokeApi") outputs.Properties = new OrdinalDictionary<SwaggerSchema> { { "body", Schema("outputSchema", false) } };
        return new ServiceOperationManifest
        {
            Connector = GetService(), InputsLocation = [InputsLocation.Inputs, InputsLocation.Parameters],
            Inputs = new SwaggerSchema { Type = SwaggerSchemaType.Object, Properties = fields,
                Required = operationId == "invokeApi" ? ["apiName", "apiVersion", "operationId"] : [] },
            Outputs = outputs,
            ConnectionReference = new ConnectionReferenceFormat { ReferenceKeyFormat = ConnectionReferenceKeyFormat.ServiceProvider },
            Settings = new OperationManifestSettings { SecureData = new OperationManifestSettingWithOptions<SecureDataOptions>() }
        };
    }

    private static SwaggerSchema Selector(string title, string operationId, params string[] dependencies) => new()
    {
        Type = SwaggerSchemaType.String, Title = title,
        DynamicList = new DynamicStateCustomDynamicList
        {
            DynamicState = new DynamicState { OperationId = operationId },
            Parameters = Parameters(dependencies)
        }
    };

    private static SwaggerSchema Schema(string operationId, bool input) => new()
    {
        Type = SwaggerSchemaType.Object,
        DynamicProperties = new DynamicStateCustomDynamicProperties
        {
            OperationId = operationId, DynamicState = new DynamicState { Extension = new DynamicState { OperationId = operationId }, IsInput = input },
            Parameters = Parameters("apiName", "apiVersion", "operationId")
        }
    };

    private static OrdinalDictionary<SwaggerCustomDynamicParameter> Parameters(params string[] names)
    {
        var parameters = new OrdinalDictionary<SwaggerCustomDynamicParameter>();
        foreach (var name in names) parameters.Add(name, new SwaggerCustomDynamicParameter { ParameterReference = name, Required = true });
        return parameters;
    }
}
