# Azure Portal PoC: source findings and execution plan

[Nederlands](../nl/azure-portal-poc.md)

Revision: 2026-10-14. Related: [issue #2](https://github.com/max1weber/logicapps-standard-apim-connector/issues/2). Status: source investigation and fixtures, not a completed PoC.

## Findings
The inspected Microsoft Standard designer implementation has dynamic list/schema calls through hostruntime operationGroups and dynamicInvoke. It passes selected parameters and connection configuration. Input/output schema processing exists. This supports trying a built-in service provider; it does not prove a custom extension can register the necessary dispatch handlers or that the deployed Azure Portal version matches this source.

The current documented GetService/GetOperations/InvokeOperation surface alone does not establish the full dynamic-handler contract. Determine the SDK interfaces and manifest representation before implementing them. Do not copy managed-connector x-ms-dynamic-values metadata into a service-provider manifest and assume it works.

Developer-authorised access remains required. Receiving a connection configuration does not prove the runtime receives the developer's identity. A host managed identity alone can broaden discovery permissions. Verify caller authorisation and token handling explicitly.

## Included fixtures
- Synthetic Orders v1/v2 OpenAPI contracts, with GetOrder and CreateOrder.
- An ARM template adding a version set and both API versions to an existing test APIM, with mock responses.
- A machine-readable evidence log with source commits, observations and unproven items.

The API-wide mock policy always returns HTTP 200, including POST; it is for schema/discovery checks, not a valid CreateOrder business implementation or runtime test. Subscription requirements and inherited parent policies remain in effect. The template embeds copies of the OpenAPI contracts; keep these copies synchronised.

## Prepare APIM fixtures
Use a dedicated test APIM in a known resource group. The template writes only the named PoC API resources, but can overwrite existing resources with the same names. Select a unique prefix and inspect what-if before deploying. No Azure resources have been deployed by this change.

Example from repository root, after installing/authenticating Azure CLI:

```bash
az deployment group what-if --resource-group YOUR_TEST_RESOURCE_GROUP --template-file examples/azure-portal-poc/apim-orders.arm.json --parameters apimServiceName=YOUR_TEST_APIM pocName=YOUR_UNIQUE_PREFIX
az deployment group create --resource-group YOUR_TEST_RESOURCE_GROUP --template-file examples/azure-portal-poc/apim-orders.arm.json --parameters apimServiceName=YOUR_TEST_APIM pocName=YOUR_UNIQUE_PREFIX --mode Incremental
```

These commands have not been executed or validated against Azure here. Do not use complete deployment mode. A sample API subscription key is not a discovery authorisation mechanism.

## Remaining implementation sequence
1. In an environment with .NET/Functions tooling, resolve current supported service-provider packages and inspect the real dynamic-handler SDK interfaces.
2. Build the smallest extension registering one action and one internal list handler. Prove hostruntime dynamicInvoke dispatch before implementing APIM discovery.
3. Add version/operation selectors and input/output schema handlers using the proven SDK contract.
4. Connect handlers to APIM management reads with developer-authorised discovery. Record credentials boundary and exact required permissions.
5. Install in a test Standard app and record runtime, package, extension-bundle and Portal designer versions.
6. Select Orders/v1/GetOrder, verify orderId input and response tokens, save/reopen; switch to v2 and verify status field.
7. Add an API/version and refresh without rebuilding. Change upstream selections and verify stale values are cleared. Demonstrate revoked discovery access.
8. Extend inventory coverage with an unversioned API and enough inventory to exercise pagination; these fixtures are not included yet.
9. Record actual results and limitations. Keep #2 open until the mandatory Azure criteria pass.

## Verification and current blockers
Fixture JSON parses; embedded contracts match the standalone files; policy XML parses; v1/v2 operation identities and schema differences are inspected. This is structural validation, not an ARM semantic validation or a designer test.

No .NET SDK, Azure CLI, Functions tooling or authenticated Azure test environment is available in this session. Package retrieval from NuGet failed. Therefore compilation, deployment and the Portal demo are not run. No framework/package version or new SDK method has been invented.

## Source evidence
- https://github.com/Azure/LogicAppsUX/blob/328a2543863484f1e41358b649bfdbb24c9ed7f2/libs/logic-apps-shared/src/designer-client-services/lib/standard/connector.ts
- https://github.com/Azure/LogicAppsUX/blob/328a2543863484f1e41358b649bfdbb24c9ed7f2/libs/logic-apps-shared/src/designer-client-services/lib/base/connector.ts
- https://github.com/Azure/LogicAppsUX/blob/328a2543863484f1e41358b649bfdbb24c9ed7f2/libs/logic-apps-shared/src/designer-client-services/lib/standard/operationmanifest.ts
- https://github.com/Azure/LogicAppsUX/blob/328a2543863484f1e41358b649bfdbb24c9ed7f2/libs/designer-v2/src/lib/core/utils/parameters/dynamicdata.ts
- https://learn.microsoft.com/en-us/azure/logic-apps/custom-connector-overview
- https://learn.microsoft.com/en-us/azure/logic-apps/create-custom-built-in-connector-standard
