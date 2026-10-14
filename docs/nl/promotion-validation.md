# Een workflow controleren vóór promotie

Het commando `validate-workflow` is een alleen-lezen-controle vóór deployment voor opgeslagen APIM-connectoracties. Het controleert de API-inventaris en geëxporteerde contracten van de doel-APIM met een ARM-token waarvoor de developer bevoegd is. Het voert geen deployment uit, herschrijft geen workflow, vernieuwt geen bindings en roept geen businessgateway aan. Runtime blijft onafhankelijk van dit commando en ARM-toegang.

## De controle uitvoeren

Bouw vanuit `develop` en geef de doelsubscription, resourcegroep, APIM-service en het workflowbestand op:

```bash
dotnet build src/Connector.Discovery.Cli/Connector.Discovery.Cli.csproj --configuration Release
# Zet APIM_DEVELOPER_ARM_TOKEN via je credentialmechanisme in de procesomgeving.
# Commit nooit een token en zet het niet in commandoregelargumenten.
dotnet run --project src/Connector.Discovery.Cli/Connector.Discovery.Cli.csproj --configuration Release -- \
  00000000-0000-0000-0000-000000000001 target-resource-group target-apim \
  validate-workflow path/to/workflow.json
```

Exitcode `0` betekent dat iedere gevonden APIM-actie is goedgekeurd. Exitcode `1` blokkeert deployment; eerdere uitvoer `Compatible` vóór een latere fout betekent niet dat de workflow is goedgekeurd. Ongeldige argumenten geven `2`. Neem het commando op als verplichte deploymentstap die moet slagen vóór upload. Workflows zonder opgeslagen APIM-bindings geven `NoApimBindingsFound`, zodat niet per ongeluk het verkeerde bestand wordt goedgekeurd.

Alle connectoracties in het bestand moeten deze ene APIM-service gebruiken. Meerdere verbindingen met die service kunnen verschillende runtime-audiences en identiteiten hebben. Voor workflows met verschillende APIM-services is een koppeling tussen verbinding en doel nodig; dit previewcommando biedt die niet. Gebruik het daarvoor niet.

## Opgeslagen bindings en omgevingsconfiguratie

Een actie bevat een letterlijke `_binding`-snapshot onder `inputs.parameters.payload`; de opgeslagen selecties `apiName`, `apiVersion` en `operationId` moeten daarmee overeenkomen. Expressies in deze velden kunnen niet statisch worden gecontroleerd. Ook acties in scopes, voorwaarden en lussen worden gelezen. Ontbrekende of verouderde snapshots geven een expliciete fout. Het commando leest het bestand zonder te schrijven.

Houd de workflow gelijk bij promotie. Configureer het verbindingsprofiel, de gateway-URL, business-audience, authenticatie-identiteit en optionele subscription key in de doelomgeving. Gatewayconfiguratie staat los van de snapshot. Bij versionsets moeten de resourcenaam van de versionset en de gekozen versie gelijk blijven tussen omgevingen; de API-resourcenaam mag verschillen. Onversioneerde API's vereisen dezelfde API-resourcenaam. Operatie-identifiers blijven gelijk.

Het doel wordt gevonden via familie, versie en operatie; routing en genormaliseerde invoer-/uitvoerschemas worden met de snapshot vergeleken. Ontbrekende families, versies of operaties geven `TargetApiMissing`, `TargetApiVersionMissing` of `TargetOperationMissing`. Een dubbelzinnige versie geeft `TargetApiVersionAmbiguous`. Routingwijzigingen geven `TargetRoutingChanged`; schemawijzigingen geven `TargetInputContractChanged` of `TargetOutputContractChanged`.

## Voorzichtige compatibiliteitsregels

Compatibel: dezelfde methode, API-pad, operatie-URL-template en versierouting, met equivalente schemas. Volgorde van objectvelden en van elementen in `required` en `enum` maakt niet uit. Wijzigingen in schema-annotaties `title`, `description`, `example`, `examples` en `externalDocs` worden genegeerd. API-resourcenamen binnen dezelfde versionset en versie mogen verschillen.

Alle andere schemawijzigingen blokkeren promotie, inclusief optionele velden, enumuitbreidingen, defaults, beperkingen en extensies. Sommige daarvan kunnen voor een specifieke toepassing veilig zijn, maar de preview vereist expliciete beoordeling en leidt niet zelf compatibiliteit per richting af. Een werkelijk contractveld met de naam `description` blijft onderdeel van de vergelijking.

Vernieuw bij een bewuste contractwijziging de operatie expliciet in de designer, beoordeel de workflowdiff, test en voer de controle opnieuw uit. De controle vervangt nooit stilzwijgend het opgeslagen contract door het doelcontract.

## Wat hiermee is bewezen

CI controleert compatibiliteit bij alleen annotatiewijzigingen, afwijzing van gewijzigde selecties/routing/schemas, ongewijzigd lezen van workflows en echte discovery-clientaanroepen tegen een synthetische ARM-/exportgrens. Ook de CLI en bestaande adapter-/packagetests worden gebouwd en uitgevoerd. Dit bewijst een uitvoerbare voorafcontrole, geen echte DEV-naar-TST-deployment.

Promotie van een ongewijzigde, opgeslagen Portal-/VS Code-workflow vereist nog Azure-validatie van snapshotopslag, doelidentiteit, gatewayrouting en netwerktoegang. De controle bewijst geen business-API-autorisatie, bereikbaarheid, APIM-policies, runtime-retry/history of actualiteit na het interval tussen controle en deployment. Voer de controle kort vóór deployment uit en beheers configuratiewijzigingen via deploymentcontroles.
