# Epic draft

[Nederlands](../nl/epic.md)

## English
### Outcome
A reusable community connector for Logic Apps Standard: developers select an APIM connection, discover APIs live, choose API/version/operation, and obtain typed input/output fields. New APIs appear on refresh without rebuilding the connector. Runtime calls the gateway using environment-specific configuration and requires no management-plane access for business calls.

### User story
As a Logic Apps developer, I want to discover and select an APIM API version and operation in the designer, so I can compose typed workflows and promote unchanged workflow definitions across DEV/TST/ACC/PRD.

### Delivery checklist
- [ ] Prove dependent API/version/operation dropdowns and dynamic schemas in VS Code and Azure Portal.
- [ ] Establish a supported SDK/runtime matrix and designer authentication flow.
- [ ] Implement live discovery including paging, version sets, unversioned APIs and explicit refresh.
- [ ] Separate discovery read permissions from runtime business API authentication.
- [ ] Persist stable operation bindings and contract snapshots without environment hostnames.
- [ ] Implement REST/JSON execution, managed identity, optional subscription keys, path/query/header version routing and meaningful errors.
- [ ] Validate retry/timeout/runAfter/secure-history behaviour in Standard.
- [ ] Prove promotion of the same workflow to two environments.
- [ ] Organise implementation as vertical slices with feature-owned handlers, validation and tests.
- [ ] Deliver complete English/Dutch documentation and clean-project installation verification.
- [ ] Package and publish reproducible releases with compatibility matrix and changelog.
- [ ] Resolve the connector licence: owner wants improvements contributed upstream; existing MIT does not require this.
- [ ] Complete Matt Pocock per-repository setup after reviewing installed skills.

### Definition of done
A new API/version becomes selectable without a connector rebuild; selected schemas are typed; runtime still succeeds when management access is disabled; a clean-project installation works from the guide alone; documentation pairs match; no organisation-specific inventory or secrets are published. If the exact designer UX is unsupported, document evidence and obtain an explicit product decision before substituting another UX.

### Architecture
Slices: Discovery/ListApis, ListVersions, ListOperations, GetOperationSchema; Execution/InvokeOperation; Deployment/ValidateBinding. Host adapters connect supported SDK interfaces to these use cases. Strict identity isolation, if required, uses a separate discovery host.

