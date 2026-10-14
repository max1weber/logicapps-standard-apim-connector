# Discovery implementation

[Nederlands](../nl/discovery-implementation.md)

Revision: 2026-10-14. Part of Azure Portal PoC #2. This is executable discovery logic and a CLI, not a registered Standard connector yet.

## Implemented

- ARM API inventory, all pages, current revisions only.
- Versions grouped by APIM version-set identity; unversioned APIs remain selectable.
- Operation names, methods and URL templates.
- Version-set routing metadata.
- OpenAPI 3 JSON export through a temporary SAS URL. The ARM bearer token is never added to the storage request.
- Per-request developer token, scoped pagination, error redaction and bounded JSON documents.

The caller supplies a developer ARM token. There is no implicit host managed identity, no delegated token exchange and no shared cache. Visibility is governed by that token's actual ARM permissions; a service-wide list permission can expose all API metadata in that service. This does not establish API-specific authorisation or designer token forwarding. Transport redirects must be disabled; the CLI does this.

## Build and run

Install .NET SDK 8. The core uses net8.0 without third-party packages; this target is not a claim that the Standard service-provider SDK supports that framework. Its adapter/runtime compatibility is still pending.

```bash
dotnet build src/Connector.Discovery.Cli/Connector.Discovery.Cli.csproj --configuration Release
dotnet run --project tests/Connector.Discovery.Tests/Connector.Discovery.Tests.csproj --configuration Release
```

Authenticate Azure CLI as the intended developer and set a short-lived token without printing it:

```bash
export APIM_DEVELOPER_ARM_TOKEN="$(az account get-access-token --resource https://management.azure.com/ --query accessToken -o tsv)"
dotnet run --project src/Connector.Discovery.Cli -- SUBSCRIPTION_GUID RESOURCE_GROUP APIM_NAME apis
dotnet run --project src/Connector.Discovery.Cli -- SUBSCRIPTION_GUID RESOURCE_GROUP APIM_NAME versions API_RESOURCE_NAME
dotnet run --project src/Connector.Discovery.Cli -- SUBSCRIPTION_GUID RESOURCE_GROUP APIM_NAME operations API_RESOURCE_NAME
dotnet run --project src/Connector.Discovery.Cli -- SUBSCRIPTION_GUID RESOURCE_GROUP APIM_NAME export API_RESOURCE_NAME
unset APIM_DEVELOPER_ARM_TOKEN
```

Use the API resource name returned by the inventory, not its display name. Contract output may contain private schema information; keep organisational output private. The CLI reads only management resources and temporary exports, never mutates APIM. Export storage is limited to public-Azure blob hostnames; sovereign clouds are outside this PoC.

## Verification boundary

The console test harness exercises the public discovery client using deterministic ARM/storage HTTP responses. It covers paginated inventory/revision exclusion, unversioned selection, hostile pagination links, 403 redaction, token separation on export, and operation routing. It is run with dotnet run, not dotnet test. These checks do not prove live ARM contracts, role assignments, package installation or Portal rendering. CI builds the CLI and runs the harness; Azure validation remains separate.

## Network decision

APIM is VNet integrated and accessible privately and publicly through the same DNS name. Keep one gateway URL per environment; Standard-host DNS/routing decides the runtime path. Discovery uses management.azure.com, separate from that gateway. Split DNS, private routing and the identity boundary must still be verified in the test environment.

## Remaining work

Register the actual service-provider SDK adapter and dynamicInvoke handlers, prove the Portal authoring flow with developer-authorised discovery, normalise selected operation schemas, persist bindings, and validate the deployed environment. GetVersionRoutingAsync is available to the adapter but not yet exposed as a CLI command. API/operation enumeration and full export do not yet constitute dynamic input/output field rendering.

References: [APIM export](https://learn.microsoft.com/en-us/rest/api/apimanagement/api-export/get?view=rest-apimanagement-2024-05-01), [Standard extensibility](https://learn.microsoft.com/en-us/azure/logic-apps/custom-connector-overview).
