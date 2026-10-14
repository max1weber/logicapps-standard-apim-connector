# Discovery-implementatie

[English](../en/discovery-implementation.md)

Revisie: 2026-10-14. Onderdeel van Azure Portal-PoC #2. Dit is uitvoerbare discoverycode met CLI, nog geen geregistreerde Standard-connector.

## Geïmplementeerd

- ARM-API-inventaris, alle pagina’s, alleen huidige revisies.
- Versies op basis van APIM-versionset; API’s zonder versie blijven selecteerbaar.
- Operatienamen, HTTP-methodes en URL-templates.
- Versieroutingmetadata.
- OpenAPI-3-JSON-export via tijdelijke SAS-URL; ARM-token wordt niet meegestuurd naar storage.
- Developertoken per request, gescopeerde paginering, geredigeerde fouten en begrensde JSON-documenten.

De aanroeper levert een developer-ARM-token. Er is geen impliciete host managed identity, delegated token exchange of gedeelde cache. Zichtbaarheid volgt de echte ARM-rechten van het token; servicebrede lijsttoegang kan alle API-metadata tonen. Dit bewijst geen API-specifieke autorisatie of designertokendoorgifte. Transportredirects moeten uitstaan; de CLI stelt dat in.

## Bouwen en uitvoeren

Installeer .NET SDK 8. De kern gebruikt net8.0 zonder externe packages. Dit bewijst niet dat het Standard-serviceprovider-SDK dat framework ondersteunt; adapter/runtimecompatibiliteit staat open.

```bash
dotnet build src/Connector.Discovery.Cli/Connector.Discovery.Cli.csproj --configuration Release
dotnet run --project tests/Connector.Discovery.Tests/Connector.Discovery.Tests.csproj --configuration Release
```

Meld Azure CLI aan als de bedoelde developer en stel een kortlevend token in zonder het af te drukken:

```bash
export APIM_DEVELOPER_ARM_TOKEN="$(az account get-access-token --resource https://management.azure.com/ --query accessToken -o tsv)"
dotnet run --project src/Connector.Discovery.Cli -- SUBSCRIPTION_GUID RESOURCE_GROUP APIM_NAME apis
dotnet run --project src/Connector.Discovery.Cli -- SUBSCRIPTION_GUID RESOURCE_GROUP APIM_NAME versions API_RESOURCE_NAME
dotnet run --project src/Connector.Discovery.Cli -- SUBSCRIPTION_GUID RESOURCE_GROUP APIM_NAME operations API_RESOURCE_NAME
dotnet run --project src/Connector.Discovery.Cli -- SUBSCRIPTION_GUID RESOURCE_GROUP APIM_NAME export API_RESOURCE_NAME
unset APIM_DEVELOPER_ARM_TOKEN
```

Gebruik de resourcenaam uit de inventaris, niet de displaynaam. Contractoutput kan privé-schema’s bevatten; houd organisatieoutput privé. De CLI leest alleen managementresources en tijdelijke exports en wijzigt APIM niet. Exportstorage is beperkt tot publieke Azure-blobhostnamen; sovereign clouds vallen buiten deze PoC.

## Verificatiegrens

Het consoletestprogramma controleert de publieke discoveryclient met deterministische ARM/storage-HTTP-responses: paginering/revisiefilter, onversioneerde selectie, onveilige paginalinks, 403-redactie, tokenscheiding bij export en operatierouting. Uitvoering is via dotnet run, niet dotnet test. Dit bewijst geen live ARM-contracten, roltoewijzingen, package-installatie of Portalweergave. CI bouwt de CLI en draait de controles; Azure-validatie staat apart.

## Netwerkbesluit

APIM is VNet-geïntegreerd en via dezelfde DNS-naam intern en publiek bereikbaar. Eén gateway-URL per omgeving; DNS/routing van de Standard-host bepaalt de runtime-route. Discovery gebruikt management.azure.com, los van de gateway. Split DNS, privérouting en identiteitsscheiding moeten nog in de testomgeving worden bewezen.

## Resterend werk

Registreer de echte serviceprovider-SDK-adapter en dynamicInvoke-handlers, bewijs Portalauthoring met developerautorisatie, normaliseer geselecteerde schemas, sla bindings op en valideer deployment. GetVersionRoutingAsync is voor de adapter beschikbaar maar nog geen CLI-commando. API-/operatielijsten en volledige export leveren nog geen dynamisch getekende invoer-/uitvoervelden.

Bronnen: [APIM-export](https://learn.microsoft.com/en-us/rest/api/apimanagement/api-export/get?view=rest-apimanagement-2024-05-01), [Standard-extensibility](https://learn.microsoft.com/en-us/azure/logic-apps/custom-connector-overview).
