# Validate a workflow before promotion

The `validate-workflow` command is a read-only pre-deployment gate for saved APIM connector actions. It validates the target APIM inventory and exported contracts using a developer-authorized ARM token. It does not deploy, rewrite the workflow, refresh bindings, or call the business gateway. Runtime remains independent of this command and of ARM access.

## Run the gate

Build from `develop`, then supply the target subscription, resource group, APIM service and workflow file:

```bash
dotnet build src/Connector.Discovery.Cli/Connector.Discovery.Cli.csproj --configuration Release
# Set APIM_DEVELOPER_ARM_TOKEN in the process environment via your credential mechanism.
# Never commit a token or include it in command-line arguments.
dotnet run --project src/Connector.Discovery.Cli/Connector.Discovery.Cli.csproj --configuration Release -- \
  00000000-0000-0000-0000-000000000001 target-resource-group target-apim \
  validate-workflow path/to/workflow.json
```

Exit code `0` means every discovered APIM action passed. Exit code `1` blocks deployment; partial `Compatible` output before a later failure does not mean the workflow passed. Invalid arguments return `2`. Wire the command as a mandatory deployment step whose success is required before upload. Workflows without saved APIM bindings fail with `NoApimBindingsFound`, preventing accidental validation of the wrong file.

All connector actions in the supplied workflow must target this one APIM service. Multiple connections to that service can have different runtime audiences/identities. Workflows targeting different APIM services require a connection-to-target mapping; this preview command does not provide that mapping. Do not use it for such workflows.

## Saved bindings and environment configuration

An action must contain a literal `_binding` snapshot under `inputs.parameters.payload` and its saved `apiName`, `apiVersion` and `operationId` selections must match. Expressions in these fields cannot be statically validated. Actions nested in scopes/conditions/loops are inspected. Missing or stale snapshots fail explicitly. The command reads the file without writing it.

Keep the workflow unchanged when promoting. Configure the connection profile, gateway URL, business audience, authentication identity and optional subscription key in the target environment. Gateway configuration is separate from the snapshot. For version sets, the version-set resource name and selected version must be stable between environments; the target API resource name may differ. Unversioned APIs require the same API resource name. Operation identifiers must be stable.

The target is resolved by family/version/operation, and its routing and normalized input/output schemas are compared with the saved snapshot. Missing families, versions or operations return `TargetApiMissing`, `TargetApiVersionMissing` or `TargetOperationMissing`. Ambiguous versions fail with `TargetApiVersionAmbiguous`. Routing changes fail with `TargetRoutingChanged`; schema changes return `TargetInputContractChanged` or `TargetOutputContractChanged`.

## Conservative compatibility rules

Compatible: identical method, API path, operation URL template and version routing, with equivalent schemas. Object-property order and the order of `required` and `enum` entries do not matter. Changes to schema `title`, `description`, `example`, `examples` and `externalDocs` are ignored. API resource names within the same version-set/version can vary.

Every other schema change blocks promotion, including optional fields, enum expansion, defaults, constraints and extensions. Some of these may be safe for a specific application, but the preview deliberately requires explicit review instead of inferring directional compatibility. A property actually named `description` remains a contract field and is compared.

For an intentional contract update, use the designer to reselect/refresh the operation explicitly, review the resulting workflow diff, test it and rerun validation. The gate never silently substitutes the target contract for the saved one.

## What this proves

CI exercises annotation-only compatibility, rejected selection/routing/schema changes, unchanged workflow reads, and real discovery-client requests against a synthetic ARM/export boundary. It also builds the CLI and runs the existing adapter/package tests. This establishes executable preflight behavior, not a live DEV-to-TST deployment.

Actual promotion of an unchanged saved Portal/VS Code workflow still requires Azure validation of snapshot persistence, target identity, gateway routing and network access. The gate does not prove business API authorization, connectivity, APIM policies, runtime retry/history behavior or freshness after the validation-to-deployment interval. Rerun close to deployment and manage configuration changes through deployment controls.
