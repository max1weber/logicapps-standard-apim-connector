# Installation

[Nederlands](../nl/installation.md)

Revision: 2026-10-14. A buildable experimental preview is available; Azure/Portal installation is not yet validated. See [Standard adapter preview](standard-adapter.md) for build, settings, test deployment, verification and rollback.

## Prepare the repository

Clone the repository using Git and open it in your development agent. Project-local Matt Pocock skills are included under .agents/skills with their supporting files; they are not installed globally. See [agent skills](agent-skills.md).

## Connector installation: release requirements

Executable commands will be added after the PoC establishes the runtime/SDK matrix. The validated procedure must cover:

1. Required Standard runtime, supported OS/hosting, .NET, Functions Core Tools and VS Code extension versions.
2. Extension installation and registration in new and existing Standard projects, packaging and Azure deployment.
3. Discovery-host deployment if a separate service is selected.
4. APIM-scoped discovery read/export permissions and authenticated designer access.
5. Runtime managed identity, business API audience and optional Key Vault-backed subscription key.
6. Connection configuration and environment-specific app settings.
7. Private APIM DNS, VNet integration and routing.
8. Local developer authentication and discovery refresh.
9. Verification in VS Code and Azure Portal: select API/version/operation, check typed fields, execute an API call.
10. Promote unchanged workflow content from DEV to TST; change only environment configuration.
11. Upgrade, rollback, uninstall and troubleshooting.

## Acceptance

Follow the guide in a clean Standard project. Discover a newly added API/version without rebuilding the extension. Execute a saved operation while discovery management access is disabled. Verify retries, timeouts and secure history. Unsupported designer behaviours must be documented before release; do not claim installation is validated yet.

## Deployment preflight

Before promoting a saved workflow, run the read-only [target contract validation](promotion-validation.md). It blocks missing selections and changed contracts without rewriting the workflow.
