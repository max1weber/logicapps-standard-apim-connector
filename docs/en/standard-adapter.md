# Standard adapter preview

[Nederlands](../nl/standard-adapter.md)

Revision: 2026-10-14. Work towards epic #1 and Azure Portal PoC #2.

## Implemented and verified in CI
The extension registers apimconnector through WebJobsStartup and ServiceOperationsProvider. The actual pinned SDK (Microsoft.Azure.Workflows.WebJobs.Extension 1.170.91, .NET 8) routes custom dynamicInvoke calls to InvokeOperation. The manifest offers API-family, API-version and operation selectors, plus dynamic input/output schemas. SDK serialization tests verify handler references and dependencies.

The API family selector uses a version-set name (or an unversioned API name), independent of subscription/resource group. A version selection retains the APIM API resource name. Names and API paths must match across target environments.

Schema discovery creates a hidden _binding default containing operation metadata and schemas, with no gateway URL or credentials. Runtime checks API-family/version/operation selections against that snapshot, obtains a business token from the SDK and performs HTTPS gateway calls without ARM discovery. API path/query/header version routing and static query constraints are retained; unsupported non-scalar path/query/header serialization is rejected at discovery.

The runtime validates types, required fields and enums. It does not implement every JSON Schema numeric/length/format constraint. Gateway/backend validation remains necessary. Schema composition, recursion, external references and non-JSON content are explicitly unsupported in this preview. The executor has no automatic retries; HTTP errors are redacted and Retry-After is returned to the SDK. Actual workflow retry/timeout/runAfter and secure history require host validation.

CI builds the core, CLI and adapter, runs the discovery and SDK/runtime scenario programs, publishes a clean example host, and checks its extensions.json plus both custom DLLs in the NuGet package. This is not proof of Azure Portal rendering, hidden-default persistence, live developer permissions, managed identity selection, or private DNS/routing.

## Build and obtain the preview
Use .NET SDK 8:

```bash
dotnet run --project tests/Connector.Discovery.Tests --configuration Release
dotnet run --project tests/Connector.Adapter.Tests --configuration Release
dotnet publish examples/standard-host/StandardHost.csproj --configuration Release --output out/standard-host
```

Alternatively download the standard-adapter-preview artifact from a successful GitHub Actions run. It includes the publish output and experimental .nupkg. No NuGet.org publication or supported release is claimed. The connector licence decision remains open; third-party licences remain in force.

## Configure a dedicated test app
Use a Logic Apps Standard .NET 8 in-process test app, Functions runtime v4. Use FUNCTIONS_WORKER_RUNTIME=dotnet and FUNCTIONS_INPROC_NET8_ENABLED=1. Configure storage and managed identity. The example is NuGet based, without extensionBundle in host.json; do not combine it with an unrelated bundle version. Preserve the existing app's storage/VNet settings.

The example connections.json has the logical connection apim and a connectionString reference to APIM_CONNECTION_PROFILE. Set that app setting to JSON such as:

```json
{
  "subscriptionId": "YOUR_SUBSCRIPTION_GUID",
  "resourceGroup": "YOUR_TEST_RESOURCE_GROUP",
  "serviceName": "YOUR_TEST_APIM",
  "developerTokenSetting": "APIM_DISCOVERY_TOKEN_DEVELOPER",
  "gatewayBaseUrl": "https://YOUR_GATEWAY_HOSTNAME",
  "audience": "api://YOUR_BUSINESS_API_APPLICATION",
  "subscriptionKeySetting": "APIM_SUBSCRIPTION_KEY"
}
```

subscriptionKeySetting is optional. Store keys through Key Vault-backed settings. The business identity needs the API's Entra authorisation; APIM ARM RBAC alone is insufficient.

For discovery, set APIM_DISCOVERY_TOKEN_DEVELOPER to a short-lived ARM token acquired as the intended developer. The token must never be committed or placed in the workflow. A configured host token is a manual PoC mechanism, not interactive sign-in, impersonation of the Portal caller or a secure multi-developer authorisation boundary. Do not use it as production user isolation. Different users must not inherit a privileged shared profile. Production delegated authorisation is still unresolved.

For local development, copy local.settings.example.json to local.settings.json and fill only local values. Start Azurite for sample storage, then Functions Core Tools v4. Local credentials and settings are excluded from publishing. Local runtime startup itself has not been exercised here.

## Azure Portal test procedure
1. Inspect a deployment what-if for the synthetic Orders fixture before using a dedicated test APIM.
2. Deploy only the published example output to the dedicated Standard test app (zip-deploy into the app root, not its parent folder). An example command is az webapp deployment source config-zip --resource-group YOUR_TEST_GROUP --name YOUR_TEST_APP --src YOUR_PUBLISH_ZIP. This has not been executed against Azure here; zip-deploy replaces app content, so use a dedicated test app.
3. Restart and confirm the custom startup registration loads. Open GetOrder and add API Management / Invoke an API operation.
4. Select the connection/profile, API family, version and operation. Confirm orderId and the correct version's response fields appear.
5. Save/reopen and verify payload._binding is retained. If absent, stop: the current runtime intentionally fails BindingSnapshotMissing rather than calling ARM.
6. Connect orderId to the request trigger and use body.id in a response action. Enable secure inputs/outputs where sensitive data is involved; check actual run history.
7. Execute the workflow, revoke discovery ARM access and rerun the saved workflow. Business execution must still work.
8. Add a new API/version, refresh and verify it appears without an extension rebuild. Change the API family with stale downstream values and verify binding rejection/selection invalidation.
9. Validate per-developer permissions, managed identity, retries and DNS/routing explicitly. Record results, SDK/runtime versions and evidence; keep cloud criteria open until demonstrated.

## Network, upgrade and rollback
APIM is user-confirmed VNet integrated with one DNS name reachable internally and publicly. Use that hostname in gatewayBaseUrl for the environment; Standard-host DNS/routing selects the route. ARM discovery and temporary export storage need separate reachability. Do not substitute a private IP in the HTTPS URL.

Promote the same workflow with environment settings only, after checking API names/version-set names/paths/contracts. Back up workflow/configuration and the previous deployment package before upgrades. Roll back by redeploying the prior package and its compatible settings. Remove the connector action references before uninstalling the extension; retain unrelated app settings and connections. A running release must never be silently given a changed contract.
