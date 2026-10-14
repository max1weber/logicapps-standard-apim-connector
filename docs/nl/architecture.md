# APIM Connector Architectuur

[English](../en/architecture.md)

Revision: 2026-10-14


### 1. Doel en eisen

Een open-source connector voor Azure Logic Apps Standard die tijdens het samenstellen van een workflow live beschikbare APIM-API’s ontdekt. Een tijdens de build gegenereerde catalogus is niet het primaire discoverymechanisme. Nieuwe API’s en versies moeten beschikbaar worden zonder de connector opnieuw te bouwen.

Verplichte designerervaring:

1. Selecteer de API Management-connector en connection/discoveryprofiel.
2. Haal beschikbare API’s op uit de geselecteerde APIM-instance.
3. Selecteer een API en haal vervolgens de versies op om een versie te kiezen.
4. Haal operaties op en kies bijvoorbeeld GetOrder (GET).
5. Toon de parameters, het requestschema en de responseschemas in de designer.
6. Bewaar een omgevingsonafhankelijke operatiebinding in de workflow.

“Beschikbaar” omvat alle API’s binnen de ingestelde APIM-scope die de discovery-identiteit mag uitlezen. Niet-ondersteunde API-types worden duidelijk aangegeven. Management-leesrechten bewijzen niet dat de runtime-identiteit iedere gevonden API mag aanroepen.

Afhankelijke keuzelijsten en dynamische invoer-/uitvoerschemas zijn verplichte PoC-acceptatiecriteria. Microsoft documenteert service-/operatiemetadata, maar bewijst hiermee niet dat deze exacte UX in een eigen Standard-serviceprovider werkt. We veronderstellen geen ongedocumenteerde designerextensie. Als de PoC faalt, beschrijven we de platformbeperking en alternatieven; gegenereerde acties of een generieke JSON-actie vervangen deze eis niet stilzwijgend.

### 2. Architectuur tijdens ontwerp en uitvoering

| Laag | Verantwoordelijkheid | Toegang |
|---|---|---|
| Designeradapter | Connectionselectie, API-/versie-/operatiediscovery, dynamische velden | Geauthenticeerde toegang tot discoverycomponent |
| Discoverycomponent | APIM-inventaris, versionsets, operaties en contractexport uitlezen en normaliseren | Management-plane-lees-/exportrechten |
| Runtime-extension | Invoer binden, API-token ophalen, gateway aanroepen, resultaat leveren | Business-API-/data-plane-toegang |
| Deploymenttooling | Omgeving instellen en contractcompatibiliteit controleren | Deploymentspecifieke rechten |

Voorgestelde logische architectuur: designer -> geauthenticeerde discoverycomponent -> APIM management API. Uitvoering: Standard-workflow -> ingebouwde runtime-extension -> APIM-gateway -> backend.

De fysieke locatie van discovery wordt in de PoC bepaald. Voorkeur: discovery in de extension-host als SDK, authenticatie en designerroute dit ondersteunen. Een aparte geauthenticeerde discoveryservice is het alternatief wanneer nodig. Discovery op dezelfde host kan een eigen identiteit gebruiken, maar die identiteit blijft voor de host beschikbaar; strikte scheiding van credentials vraagt een aparte service. Verschillende codepaden vormen geen procesisolatie.

Discovery biedt logische operaties ListApis, ListVersions, ListOperations en GetOperationSchema; routes en SDK-bindingen worden tijdens implementatie bepaald. Ondersteun paginering, API’s zonder versie, versionsetgroepering, stabiele identifiers en leesbare operatienamen. Metadata wordt op verzoek opgehaald, kort gecachet en expliciet vernieuwbaar gemaakt, met zichtbaar vernieuwingstijdstip. Inventaris vernieuwen vraagt geen connectorbuild. Behandel 401/403, throttling, exportfouten en niet-ondersteunde schemas expliciet.

### 3. Authenticatie en rechten

Het discoveryprofiel bevat subscription, resource group, APIM-servicenaam en authenticatiemodus. Gebruik bij voorkeur geauthenticeerde developertoegang of een geauthenticeerde discoveryservice met beperkte leesidentiteit. Test het echte authenticatiepad in VS Code en Azure Portal; veronderstel niet dat browsercredentials vanzelf worden doorgegeven.

Scope leesrechten op APIM en controleer dat inventaris- en exportendpoints daadwerkelijk toegankelijk zijn. Veronderstel niet zonder test dat een algemene Reader-rol alle gebruikte endpoints afdekt. Discovery vraagt geen schrijf-/verwijderrechten. Een gedeelde discovery-identiteit kan meer API’s zien dan een individuele developer; pas expliciete autorisatie toe en behandel haar niet als impersonatie van die developer.

Runtime gebruikt een expliciet gekozen system-assigned of user-assigned managed identity. APIM valideert Entra-audience en app-rollen of andere businessautorisatie. Optionele subscription keys gebruiken Key Vault-references. Resource-RBAC en business-API-autorisatie zijn gescheiden.

Lokale ontwikkeling gebruikt een expliciete ontwikkelauthenticatiemodus. Geen stille fallback naar developercredentials in Azure. Log geen credentials, tokens of subscription keys.

### 4. Omgevingsconfiguratie

| Instelling | Doel |
|---|---|
| APIM_BASE_URL | Runtime-gatewaybasis voor DEV/TST/ACC/PRD |
| APIM_TOKEN_AUDIENCE | Resource/audience voor business-API-token |
| APIM_IDENTITY_CLIENT_ID | Optionele user-assigned runtime-identiteit |
| APIM_SUBSCRIPTION_KEY | Optionele subscription key via Key Vault |
| APIM_DISCOVERY_PROFILE | Logische verwijzing naar discoveryconfiguratie |

Discovery-resource-identificatie en identiteit staan los van gatewayauthenticatie. Dit zijn voorgestelde namen, geen geïmplementeerd SDK-schema. Gebruik waar ondersteund serviceProviderConnections in connections.json met appsetting-verwijzingen.

Dezelfde logische connectionnaam geldt in iedere omgeving. Discovery kan tegen DEV plaatsvinden terwijl de gedeployde workflow tegen TST/ACC/PRD draait. Discovery mag de runtime-gateway niet wijzigen of een DEV-hostname in de actie vastleggen. Deployment controleert of de geselecteerde API/versie/operatie en een compatibel contract in de doelomgeving bestaan.

### 5. Workflowbinding en schemas

Bewaar tijdens het ontwerpen een omgevingsonafhankelijke binding: logische API-identificatie, API-versie, stabiele operatie-identificatie, API-basispad, operatiepad, HTTP-methode, versierouting en contractfingerprint. De precieze workflow-/manifestrepresentatie wordt tegen de SDK bewezen. Bewaar of package het voor uitvoering benodigde genormaliseerde contract, zodat runtime geen live management-API-aanroep nodig heeft.

Maak onderscheid tussen business-API-versie, APIM-revisie, connectorrelease en OpenAPI-specificatieversie. Ondersteun versies via pad, query en header op basis van versionsetmetadata. Gebruik normaal de huidige APIM-revisie. API’s zonder versie moeten ook selecteerbaar zijn.

Combineer gatewaybasis, API-pad en operatiepad deterministisch. OpenAPI-server-URL’s mogen de ingestelde gateway niet vervangen. Voorkom dubbele prefixes, onveilige padinterpolatie en onbedoeld overschrijven van authenticatie-/versieheaders.

Een gewijzigde selectie maakt afhankelijke selecties en velden ongeldig. Metadata vernieuwen mag bestaande workflowbindings niet stilzwijgend herschrijven. Meld contractwijzigingen; bestaande acties bijwerken is expliciet. Niet-ondersteunde schemas geven bruikbare foutmeldingen. Eerste scope: REST/JSON, path/query/headerparameters en JSON-request-/responseschemas. Multipart, streaming, SOAP, callbacks en automatische paginering volgen later.

### 6. Runtimegedrag en netwerk

Runtime vraagt voor businessaanroepen geen managementrechten. Discovery tijdens ontwerp vraagt die wel. Bij dezelfde host worden de gezamenlijke hostrechten eerlijk gedocumenteerd.

Private APIM vraagt passende routing, DNS en VNet-integratie voor runtime. Het discoverypad moet apart het managementendpoint en eventueel de discoveryservice bereiken. Een private runtime-gateway betekent niet vanzelf dat inventarisdiscovery via die gateway loopt.

Niet-2xx laat de actie falen met status en geredigeerde details. Verifieer retrybeleid, timeouts, runAfter en secure inputs/outputs met de echte Standard-runtime. Geen verborgen connectorretries bovenop workflowretries. POST-retries vragen expliciet beleid en waar nodig idempotentie. Log correlatie-ID, logische operatie, duur, status en contractfingerprint; standaard geen bodies of gevoelige waarden.

### 7. Publieke repository en tweetalige documentatie

Voorgestelde repository: logicapps-standard-apim-connector. Publiceer generieke code, fictieve Orders-v1/v2-voorbeelden en installatie-instructies. Organisatieconfiguratie, inventaris en contracten blijven privé. Licentiekeuze staat open: de repository heeft MIT, maar de eigenaar wil verbeteringen terug. Beoordeel dit apart; behoud licenties van skills van derden. Dit is een community-extension die per Standard-project wordt geïnstalleerd, zonder automatische Microsoft-gallerypublicatie.

| Pad | Inhoud |
|---|---|
| src/Connector.Host/ | Composition root en Standard-serviceproviderregistratie |
| src/Connector.Features/Discovery/ListApis/ | API-discoveryslice |
| src/Connector.Features/Discovery/ListVersions/ | API-versiediscoveryslice |
| src/Connector.Features/Discovery/ListOperations/ | Operatiediscoveryslice |
| src/Connector.Features/Discovery/GetOperationSchema/ | Dynamische schemaslice |
| src/Connector.Features/Execution/InvokeOperation/ | Runtime-API-aanroepslice |
| src/Connector.Features/Deployment/ValidateBinding/ | Compatibiliteitsslice voor doelomgeving |
| src/Connector.Infrastructure/ | APIM-managementclient, tokenverwerving en HTTP-transport |
| src/Connector.Contracts/ | Kleine versioneerbare grenscontracten |
| tests/Unit/, tests/Integration/, tests/Designer/ | Geautomatiseerde en designerverificatie |
| examples/orders/, examples/logicapp/ | Fictieve contracten en voorbeeldworkflow |
| docs/en/, docs/nl/ | Volledige overeenkomende documentatiesets |
| README.md, README.nl.md | Engelse en Nederlandse introductie/quickstart |
| .github/workflows/ | Build, verificatie, packaging, release |

Ieder uitlegdocument krijgt een Engelse en Nederlandse versie: architectuur, installatie, configuratie, authenticatie, netwerk, ontwikkelhandleiding, voorbeelden, troubleshooting, upgrades, compatibiliteit, bijdragehandleiding, securitybeleid en release notes. Gebruik dezelfde bestandsnamen onder docs/en en docs/nl, taalwissellinks en een gezamenlijk revisiekenmerk. Code-identifiers blijven Engels. Behoud de canonieke licentietekst; geef Nederlandse toelichting in plaats van een onduidelijke juridische vertaling. CI controleert documentparen en links; releasereview controleert inhoudelijke gelijkwaardigheid.

### 8. Installatiehandleiding — verplicht in de repository

Maak docs/en/installation.md en docs/nl/installation.md. Beide behandelen:

1. Ondersteunde Standard-runtime, OS/hosting, VS Code-extension, Functions Core Tools en .NET/packageversies, vastgezet na PoC-validatie.
2. Installatie in nieuw en bestaand Standard-project, registratie, packaging en Azure-deployment.
3. Installatie/configuratie van een apart gehoste discoverycomponent, indien nodig.
4. Discovery-identiteit en gescopete management-leesrechten.
5. Runtime managed identity, audience en optionele subscription key.
6. Connections en app settings per omgeving met voorbeelden zonder secrets.
7. Private APIM-routing, DNS en connectiviteitstests.
8. Lokale developerauthenticatie en designerconfiguratie.
9. End-to-end-verificatie: API -> versie -> operatie -> getypeerde invoer/uitvoer -> geslaagde aanroep.
10. Promotie van DEV naar TST met dezelfde workflowinhoud.
11. Upgrade, rollback, verwijderen en troubleshooting voor ontbrekende connector, rechten, schemavernieuwing en deploymentfouten.

Voeg installatiecommando’s en manifests pas na validatie toe. Dit ontwerp is nog geen uitvoerbare installatiehandleiding en beweert niet dat een package bestaat. Releaseacceptatie vraagt installatie in een schoon project uitsluitend met de tweetalige handleiding, gecontroleerd in VS Code en Azure Portal, of expliciet gedocumenteerde platformbeperkingen.

### 9. Fasering en acceptatie

PoC: fictieve Orders-v1/v2 met GetOrder/CreateOrder; live gepagineerde inventaris; API zonder versie; afhankelijke keuzelijsten; getypeerde dynamische invoer/uitvoer; geauthenticeerde designerdiscovery; apart runtime-token; dezelfde workflow in twee omgevingen. Voeg na installatie een nieuwe API/versie toe en bewijs dat deze na vernieuwen verschijnt zonder de extension opnieuw te bouwen.

v0.1: contractsnapshots, compatibiliteitscontrole, versierouting, foutafhandeling, stabiele bindings en geverifieerde installatie. Distributie: reproduceerbare NuGet/release-artifacts, checksums, changelog en tweetalige documentatie. Organisatie-inventaris komt niet in publieke packages.

Test verwijderde API’s, contractwijzigingen, dubbele operation IDs, gewijzigde selecties, oude cache, exportfouten, 401/403/429/500, onjuiste audience, twee connections, private networking, uitgeschakelde managementtoegang tijdens workflowuitvoering en beveiligde run history. Leg bewezen SDK/runtimecompatibiliteit vast. Geen ondersteunde release zolang verplicht designergedrag onbewezen blijft.

### 10. Vertical slice solution architecture

Organiseer de solution per use case, niet met algemene Controllers/Services/Repositories-mappen. Iedere slice beheert zijn request, response, handler, validatie, mapping en tests. Deel alleen echte technische infrastructuur en stabiele grenscontracten. De lagen uit paragraaf 2 beschrijven verantwoordelijkheden en beveiligingsgrenzen, geen verplichte horizontale codelagen.

| Slice | Eigen gedrag | Verificatie |
|---|---|---|
| Discovery/ListApis | Scopeautorisatie, paginering en API-groepering | Leesscope, paginering en API’s zonder versie |
| Discovery/ListVersions | Versionset opzoeken en versie selecteren | Pad/header/query en onversioneerde routing |
| Discovery/ListOperations | Operatielijst en stabiele identificatie | Dubbele IDs en verwijderde operaties |
| Discovery/GetOperationSchema | Contract ophalen, normaliseren en getypeerde velden leveren | Ondersteunde schemas en expliciete beperkingen |
| Execution/InvokeOperation | Bindingvalidatie, request bouwen, business-token, gatewayaanroep en resultaat | URL-routing, auth, fouten, timeouts en retries |
| Deployment/ValidateBinding | Opgeslagen binding vergelijken met doelcontract | Compatibele promotie en breaking changes |

De host vertaalt ondersteunde designer-/runtime-SDK-aanroepen naar slicehandlers. Discovery en uitvoering kunnen aparte hosts krijgen als isolatie dit vraagt. SDK-specifieke DTO’s blijven in adapters; slicecontracten hangen niet af van designer-UI-types. Slices roepen niet rechtstreeks elkaars handlers aan; compositie gebeurt aan de grens. Deel beleid pas bij aantoonbare duplicatie. Geen generieke repositorylaag of verplichte MediatR-dependency.

Gebruik binnen een slice waar nuttig Request.cs, Response.cs, Handler.cs en Validator.cs, zonder lege boilerplate te eisen. Tests krijgen overeenkomstige featuremappen onder tests. Scope cachekeys op discoveryprofiel, autorisatiecontext en API/versie; deel geen inventaris met hogere privileges met een minder bevoegde aanvrager.

Voorgestelde solutionprojecten: Connector.Host, Connector.Features, Connector.Infrastructure, Connector.Contracts en bijbehorende testprojecten. Voeg Discovery.Host alleen toe als de PoC externe discovery kiest. Contracten tussen onafhankelijk gedeployde hosts worden geversioneerd. InvokeOperation is niet afhankelijk van discoveryhandlers of managementcredentials: deze slice gebruikt de opgeslagen binding/contractsnapshot en runtimeconfiguratie.


## Bronnen

- https://learn.microsoft.com/en-us/azure/logic-apps/create-custom-built-in-connector-standard
- https://learn.microsoft.com/en-us/azure/logic-apps/custom-connector-overview
- https://learn.microsoft.com/en-us/azure/connectors/introduction
- https://learn.microsoft.com/en-us/azure/api-management/api-management-versions
- https://learn.microsoft.com/en-us/rest/api/apimanagement/api-export/get?view=rest-apimanagement-2024-05-01