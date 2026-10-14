# APIM Connector Architecture

[Nederlands](../nl/architecture.md)

Revision: 2026-10-14


### 1. Purpose and requirements

An open-source connector for Azure Logic Apps Standard that discovers available APIM APIs live while a developer composes a workflow. A build-generated catalogue is not the primary discovery mechanism. New APIs and versions must become discoverable without rebuilding the connector.

Required designer experience:

1. Select the API Management connector and connection/discovery profile.
2. Retrieve the available APIs from the selected APIM instance.
3. Select an API, then retrieve and select its versions.
4. Retrieve and select an operation, for example GetOrder (GET).
5. Load its parameters, request schema and response schemas into the designer.
6. Save an environment-independent operation binding in the workflow.

“Available” means all APIs within the configured APIM scope that the discovery identity is authorised to read. Unsupported API types must be clearly identified. APIM management read access does not prove that the runtime identity may invoke every discovered API.

Dependent dropdowns and dynamic input/output schemas are mandatory PoC acceptance criteria. Microsoft's built-in connector documentation establishes service/operation metadata, but does not establish that this exact UX works in a custom Standard service provider. No unsupported designer extension is assumed. If the PoC fails, report the platform limitation and propose alternatives; do not silently substitute build-generated actions or a raw JSON Invoke action.

### 2. Design-time and runtime architecture

| Layer | Responsibilities | Access |
|---|---|---|
| Designer adapter | Connection selection, API/version/operation discovery, dynamic fields | Authenticated access to discovery component |
| Discovery component | Read APIM inventory, version sets, operations and exported contracts; normalise metadata | APIM management-plane read/export permissions |
| Runtime extension | Bind input, obtain API token, call gateway and return result | Business API/data-plane access |
| Deployment tooling | Configure environment settings and check contract compatibility | Deployment-specific permissions |

Recommended logical architecture: designer -> authenticated discovery component -> APIM management API. Execution: Standard workflow -> built-in runtime extension -> APIM gateway -> backend.

The physical location of discovery is a PoC decision. Prefer an extension-hosted discovery component if the supported SDK, authentication and designer call path allow it. A separate authenticated discovery service is an alternative when necessary. A same-host discovery component can use a dedicated identity, but that identity is still available to that host; strict credential isolation requires a separate service. Do not claim process-level isolation merely because code paths use different identities.

Discovery provides logical operations such as ListApis, ListVersions, ListOperations and GetOperationSchema. Their routes and SDK bindings are implementation decisions. Support pagination, unversioned APIs, version-set grouping, stable identifiers and operation display names. Read metadata on demand; cache briefly with explicit refresh and an observable last-refresh timestamp. No connector rebuild is required to refresh inventory. Handle 401/403, throttling, export failure and unsupported schema constructs explicitly.

### 3. Authentication and permissions

Discovery profile includes subscription, resource group, APIM service name and authentication mode. Prefer authenticated developer access or an authenticated discovery service with a narrowly scoped read identity. Test the real designer authentication flow in VS Code and Azure Portal; do not assume browser credentials are automatically forwarded.

Use APIM-scoped read permissions sufficient for the inventory and export endpoints actually used. Verify all required actions rather than assuming a generic Reader role covers every endpoint. No APIM write/delete permissions are needed for discovery. A shared discovery identity may see more APIs than an individual developer; enforce an explicit authorisation policy and never treat it as impersonation of that developer.

Runtime uses an explicitly selected system-assigned or user-assigned managed identity. APIM validates the Entra audience and required app roles or other business authorisation. Optional subscription keys use Key Vault references. APIM resource RBAC and business API authorisation are separate.

Local development uses an explicit development authentication mode. Never silently fall back to a developer identity in Azure. Never log credentials, tokens or subscription keys.

### 4. Environment configuration

| Setting | Purpose |
|---|---|
| APIM_BASE_URL | Runtime gateway base URL for DEV/TST/ACC/PRD |
| APIM_TOKEN_AUDIENCE | Resource/audience for the business API token |
| APIM_IDENTITY_CLIENT_ID | Optional user-assigned runtime identity |
| APIM_SUBSCRIPTION_KEY | Optional Key Vault-backed subscription key |
| APIM_DISCOVERY_PROFILE | Logical reference to discovery configuration |

Discovery resource identifiers and its identity are configured separately from gateway authentication. These names are proposed settings, not an implemented SDK schema. Where supported, use serviceProviderConnections in connections.json with appsetting references.

Use the same logical connection name across environments. A developer may discover against DEV while the deployed workflow executes against TST/ACC/PRD. Discovery must not switch the runtime gateway or embed a DEV hostname into the action. The deployment pipeline verifies that the target environment contains the selected API/version/operation and a compatible contract.

### 5. Workflow binding and schemas

At design time, persist an environment-independent binding: logical API identifier, API version, stable operation identifier, API base path, operation path, HTTP method, version-routing metadata and contract fingerprint. The exact workflow/manifest representation must be proven against the SDK. Store or package the normalised contract required for execution so that runtime execution does not depend on a live management API call.

Distinguish business API version, APIM revision, connector release and OpenAPI specification version. Support version routing by path, query or header using APIM version-set metadata. Normally target the current APIM revision. Unversioned APIs must also be selectable.

Combine configured gateway base URL, API path and operation path deterministically. OpenAPI server URLs cannot override the configured gateway. Prevent duplicate prefixes, unsafe path interpolation and accidental overwriting of authentication/version headers.

Changing a selection invalidates dependent selections and fields. A metadata refresh must not silently rewrite existing workflow bindings. Contract changes are reported; updating an existing action is explicit. Unsupported schema constructs produce actionable messages. First release scope: REST/JSON and path/query/header parameters with JSON request/response schemas; multipart, streaming, SOAP, callbacks and automatic pagination are deferred.

### 6. Runtime behaviour and network

The runtime extension does not require APIM management permissions for business calls. The design-time discovery component does. If both share a host, document the host's combined privileges accurately.

Private APIM requires suitable routing, DNS and VNet integration for the runtime. The discovery path must separately reach the management endpoint and, when applicable, its own service. A private runtime gateway does not automatically imply that inventory discovery uses that gateway.

Non-2xx responses fail the action with status and redacted details. Validate retry policies, timeouts, runAfter and secure inputs/outputs with the actual Standard runtime. Avoid hidden connector retries layered over workflow retries. POST retries require explicit policy and, where needed, idempotency. Log correlation ID, logical operation, duration, status and contract fingerprint, excluding bodies and sensitive values by default.

### 7. Public repository and bilingual documentation

Proposed repository: logicapps-standard-apim-connector. Publish generic code, synthetic Orders v1/v2 examples and installation documentation. Keep organisation-specific configuration, inventory and contracts private. Licence decision pending: the existing repository has MIT, but the owner wants upstream improvements. Review the licence separately; retain third-party skill licences. This is a community extension installed in each Standard project, not automatic Microsoft gallery publication.

| Path | Content |
|---|---|
| src/Connector.Host/ | Composition root and Standard service-provider registration |
| src/Connector.Features/Discovery/ListApis/ | API discovery slice |
| src/Connector.Features/Discovery/ListVersions/ | API version discovery slice |
| src/Connector.Features/Discovery/ListOperations/ | Operation discovery slice |
| src/Connector.Features/Discovery/GetOperationSchema/ | Dynamic schema slice |
| src/Connector.Features/Execution/InvokeOperation/ | Runtime API invocation slice |
| src/Connector.Features/Deployment/ValidateBinding/ | Target-environment compatibility slice |
| src/Connector.Infrastructure/ | APIM management client, token acquisition and HTTP transport |
| src/Connector.Contracts/ | Small versioned cross-boundary contracts |
| tests/Unit/, tests/Integration/, tests/Designer/ | Automated and designer verification |
| examples/orders/, examples/logicapp/ | Synthetic contracts and sample workflow |
| docs/en/, docs/nl/ | Complete matching documentation sets |
| README.md, README.nl.md | English and Dutch introduction/quickstart |
| .github/workflows/ | Build, verification, packaging, release |

Every explanatory document must have an English and Dutch version: architecture, installation, configuration, authentication, networking, developer guide, examples, troubleshooting, upgrades, compatibility, contribution guide, security policy and release notes. Use matching filenames under docs/en and docs/nl, language-switch links and a shared revision marker. Code identifiers remain English. Preserve canonical licence text; provide a Dutch explanatory note rather than an ambiguous unofficial legal translation. CI checks paired document presence and links; release review checks semantic parity.

### 8. Installation guide — required repository deliverable

Create docs/en/installation.md and docs/nl/installation.md. They must cover:

1. Supported Standard runtime, OS/hosting combinations, VS Code extension, Functions Core Tools and .NET/package versions, pinned after PoC validation.
2. Installing a released extension in a new and existing Standard project, including registration, packaging and Azure deployment.
3. Installing/configuring the discovery component when separately hosted.
4. Configuring discovery identity and scoped management read permissions.
5. Configuring runtime managed identity, audience and optional subscription key.
6. Configuring connections and app settings for each environment, with secret-free examples.
7. Private APIM routing, DNS and connectivity checks.
8. Local developer authentication and local designer setup.
9. End-to-end verification: API -> version -> operation -> typed input/output -> successful API call.
10. Promotion from DEV to TST using unchanged workflow content.
11. Upgrade, rollback, uninstall and troubleshooting for missing connector, permissions, schema refresh and deployment errors.

Installation commands and manifests are added only after validation. This design is not an executable installation guide and must not claim a package already exists. Release acceptance requires a clean-project installation using only the bilingual guide, verified in VS Code and Azure Portal, or clearly documented platform limitations.

### 9. Delivery and acceptance

PoC: synthetic Orders v1/v2, GetOrder/CreateOrder; live paginated inventory; unversioned API; dependent dropdowns; typed dynamic inputs/outputs; authenticated designer discovery; separate runtime token; same workflow in two environments. Add a new API/version after connector installation and prove it appears on refresh without rebuilding the extension.

v0.1: contract snapshots, compatibility checks, supported version-routing methods, error handling, stable bindings and verified installation. Distribution: reproducible NuGet/release artifacts, checksums, changelog and bilingual documentation. Organisation inventory must not be embedded in public packages.

Test removed APIs, changed contracts, duplicate operation IDs, refreshed selections, stale caches, export failure, 401/403/429/500, wrong audience, two connections, private networking, disabled management access during workflow execution and secure run history. Pin the tested SDK/runtime matrix. Do not promote the PoC to a supported release if mandatory designer behaviour is unproven.

### 10. Vertical slice solution architecture

Organise the solution by use case, not by global Controllers/Services/Repositories folders. Each slice owns its request, response, handler, validation, mapping and tests. Share only genuine technical infrastructure and stable boundary contracts. The layers in section 2 are responsibilities and security boundaries, not mandatory horizontal code layers.

| Slice | Owned behaviour | Verification |
|---|---|---|
| Discovery/ListApis | Scope authorisation, pagination and API grouping | Read scope, paging, unversioned APIs |
| Discovery/ListVersions | Version-set lookup and version selection | Path/header/query and unversioned routing |
| Discovery/ListOperations | Operation list and stable identity | Duplicate IDs and removed operations |
| Discovery/GetOperationSchema | Contract retrieval, normalisation, typed fields | Supported schemas and explicit unsupported errors |
| Execution/InvokeOperation | Binding validation, request construction, business token, gateway call and result | URL routing, auth, errors, timeouts and retries |
| Deployment/ValidateBinding | Compare saved binding with target contract | Compatible promotion and breaking-change detection |

The host adapts supported designer/runtime SDK calls to slice handlers. Discovery and execution may have separate hosts if isolation requires it. SDK-specific DTOs stay at adapters; slice contracts do not depend on designer UI types. Slices must not call other slice handlers directly; compose at the boundary and extract shared policies only when real duplication warrants them. No generic repository layer or mandatory MediatR dependency is introduced.

Within a slice, use files such as Request.cs, Response.cs, Handler.cs and Validator.cs when useful; do not require empty boilerplate. Place tests in matching feature directories under tests. Keep cache keys scoped by discovery profile, authorisation context and API/version; never share a privileged inventory with a less privileged caller.

Proposed solution projects: Connector.Host, Connector.Features, Connector.Infrastructure, Connector.Contracts and corresponding test projects. A separate Discovery.Host is added only if the PoC selects external discovery. Contracts carry versions where independently deployed hosts communicate. The InvokeOperation slice must not depend on discovery handlers or management credentials: it consumes the persisted binding/contract snapshot and runtime configuration.


## Sources

- https://learn.microsoft.com/en-us/azure/logic-apps/create-custom-built-in-connector-standard
- https://learn.microsoft.com/en-us/azure/logic-apps/custom-connector-overview
- https://learn.microsoft.com/en-us/azure/connectors/introduction
- https://learn.microsoft.com/en-us/azure/api-management/api-management-versions
- https://learn.microsoft.com/en-us/rest/api/apimanagement/api-export/get?view=rest-apimanagement-2024-05-01