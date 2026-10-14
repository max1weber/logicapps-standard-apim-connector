# Azure Portal-PoC: bronbevindingen en uitvoeringsplan

[English](../en/azure-portal-poc.md)

Revisie: 2026-10-14. Bij [issue #2](https://github.com/max1weber/logicapps-standard-apim-connector/issues/2). Status: brononderzoek en fixtures, geen afgeronde PoC.

## Bevindingen
De onderzochte Microsoft Standard-designer gebruikt dynamische lijst-/schema-aanroepen via hostruntime operationGroups en dynamicInvoke, met geselecteerde parameters en connectionconfiguratie. Verwerking van getypeerde invoer/uitvoer bestaat. Dit ondersteunt onderzoek naar een built-in serviceprovider, maar bewijst niet dat een eigen extension de handlers kan registreren of dat Azure Portal dezelfde bronversie draait.

GetService/GetOperations/InvokeOperation bewijzen op zichzelf niet het volledige dynamic-handlercontract. Stel SDK-interfaces en manifestrepresentatie vast vóór implementatie. Neem managed-connector x-ms-dynamic-values-metadata niet over met de aanname dat die in serviceprovider-manifests werkt.

Discoveryleesrechten van de developer blijven leidend. Connectionconfiguratie ontvangen bewijst geen doorgifte van developeridentiteit. Alleen een host managed identity kan rechten verruimen. Verifieer callerautorisatie en tokens expliciet.

## Meegeleverde fixtures
- Fictieve Orders-v1/v2 OpenAPI-contracten met GetOrder en CreateOrder.
- ARM-template voor versionset en beide API-versies op een bestaande test-APIM, met mockresponses.
- Machineleesbaar bewijslog met broncommits, bevindingen en onbewezen punten.

De API-brede mockpolicy geeft altijd HTTP 200 terug, ook voor POST; dit is voor schema/discovery, geen correcte CreateOrder-businessimplementatie of runtimetest. Subscriptionvereisten en geërfde parentpolicies blijven gelden. Het template bevat kopieën van OpenAPI; houd die gelijk aan de losse contracten.

## APIM-fixtures voorbereiden
Gebruik een aparte test-APIM in een bekende resource group. Het template schrijft alleen genoemde PoC-resources, maar kan gelijknamige bestaande resources overschrijven. Kies een unieke prefix en controleer what-if. Deze wijziging heeft geen Azure-resources gedeployed.

Voorbeeld vanaf repositoryroot na installatie en authenticatie van Azure CLI:

```bash
az deployment group what-if --resource-group YOUR_TEST_RESOURCE_GROUP --template-file examples/azure-portal-poc/apim-orders.arm.json --parameters apimServiceName=YOUR_TEST_APIM pocName=YOUR_UNIQUE_PREFIX
az deployment group create --resource-group YOUR_TEST_RESOURCE_GROUP --template-file examples/azure-portal-poc/apim-orders.arm.json --parameters apimServiceName=YOUR_TEST_APIM pocName=YOUR_UNIQUE_PREFIX --mode Incremental
```

Deze commando’s zijn hier niet tegen Azure uitgevoerd of gevalideerd. Gebruik geen complete deployment mode. Een API-subscription key is geen discoveryautorisatie.

## Resterende implementatie
1. Bepaal in een omgeving met .NET/Functions-tooling de ondersteunde serviceprovider-packages en echte dynamic-handler-SDK-interfaces.
2. Bouw de kleinste extension met één actie en interne lijsthandler. Bewijs hostruntime dynamicInvoke-dispatch vóór APIM-discovery.
3. Voeg versie-/operatiekeuzes en invoer-/uitvoerschemas toe met het bewezen SDK-contract.
4. Koppel aan APIM-managementreads met discoveryautorisatie van de developer. Leg credentialgrens en precieze rechten vast.
5. Installeer in een Standard-testapp en noteer runtime, packages, extension-bundle en Portal-designerversie.
6. Kies Orders/v1/GetOrder, controleer orderId en responsetokens, sla op/heropen; schakel naar v2 en controleer status.
7. Voeg API/versie toe en vernieuw zonder rebuild. Controleer het wissen van oude vervolgselecties en ingetrokken discoverytoegang.
8. Breid inventarisdekking uit met een API zonder versie en voldoende inventaris voor paginering; die fixtures zijn nog niet inbegrepen.
9. Leg echte resultaten en beperkingen vast. Houd #2 open tot de verplichte Azure-criteria slagen.

## Verificatie en blokkades
Fixture-JSON is parsebaar; embedded contracten komen overeen met de losse bestanden; policy-XML is parsebaar; operatie-identiteiten en schemaverschillen zijn gecontroleerd. Dit is structuurcontrole, geen ARM-semantische validatie of designertest.

Deze sessie heeft geen .NET SDK, Azure CLI, Functions-tooling of geauthenticeerde Azure-testomgeving. NuGet-package ophalen mislukte. Compilatie, deployment en Portal-demo zijn daarom niet uitgevoerd. Geen framework-/packageversie of nieuwe SDK-methode is verzonnen.

## Bronbewijs
- https://github.com/Azure/LogicAppsUX/blob/328a2543863484f1e41358b649bfdbb24c9ed7f2/libs/logic-apps-shared/src/designer-client-services/lib/standard/connector.ts
- https://github.com/Azure/LogicAppsUX/blob/328a2543863484f1e41358b649bfdbb24c9ed7f2/libs/logic-apps-shared/src/designer-client-services/lib/base/connector.ts
- https://github.com/Azure/LogicAppsUX/blob/328a2543863484f1e41358b649bfdbb24c9ed7f2/libs/logic-apps-shared/src/designer-client-services/lib/standard/operationmanifest.ts
- https://github.com/Azure/LogicAppsUX/blob/328a2543863484f1e41358b649bfdbb24c9ed7f2/libs/designer-v2/src/lib/core/utils/parameters/dynamicdata.ts
- https://learn.microsoft.com/en-us/azure/logic-apps/custom-connector-overview
- https://learn.microsoft.com/en-us/azure/logic-apps/create-custom-built-in-connector-standard
