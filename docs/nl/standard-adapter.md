# Standard-adapterpreview

[English](../en/standard-adapter.md)

Revisie: 2026-10-14. Werk voor epic #1 en Azure Portal-PoC #2.

## Geïmplementeerd en in CI gecontroleerd
De extension registreert apimconnector via WebJobsStartup en ServiceOperationsProvider. De vastgezette SDK (Microsoft.Azure.Workflows.WebJobs.Extension 1.170.91, .NET 8) leidt eigen dynamicInvoke-aanroepen naar InvokeOperation. Het manifest biedt API-familie-, versie- en operatiekeuzes met dynamische invoer-/uitvoerschemas. SDK-serialisatietests controleren handlerverwijzingen en afhankelijkheden.

De API-familieselector gebruikt versionsetnaam of onversioneerde API-naam, los van subscription/resource group. De versiekeuze bewaart de APIM-API-resourcenaam. Namen en paden moeten overeenkomen tussen omgevingen.

Discovery levert een verborgen _binding-default met operatiemetadata en schemas, zonder gateway-URL of credentials. Runtime vergelijkt de drie selecties met de snapshot, haalt een businesstoken bij de SDK en roept HTTPS aan zonder ARM-discovery. Pad/query/headerversies en vaste queryvoorwaarden blijven behouden; niet-ondersteunde niet-scalaire parameter-serialisatie wordt bij discovery geweigerd.

Runtime valideert types, verplichte velden en enums. Niet iedere numerieke/lengte/format-constraint van JSON Schema is geïmplementeerd; gateway/backendvalidatie blijft nodig. Schemacompositie, recursie, externe referenties en niet-JSON-content zijn expliciet niet ondersteund. Geen automatische executor-retries; HTTP-fouten zijn geredigeerd en Retry-After gaat naar de SDK. Workflow-retries/timeouts/runAfter en beveiligde run history vereisen echte hostvalidatie.

CI bouwt kern, CLI en adapter, voert discovery- en SDK/runtimescenario’s uit, publiceert een schoon voorbeeld en controleert extensions.json en beide custom DLL’s in de NuGet-package. Dit bewijst geen Portalweergave, verborgen-defaultopslag, live developerrechten, managed identity of privé-DNS/routing.

## Preview bouwen of ophalen
Gebruik .NET SDK 8:

```bash
dotnet run --project tests/Connector.Discovery.Tests --configuration Release
dotnet run --project tests/Connector.Adapter.Tests --configuration Release
dotnet publish examples/standard-host/StandardHost.csproj --configuration Release --output out/standard-host
```

Of download standard-adapter-preview uit een geslaagde GitHub Actions-run. Deze bevat publishoutput en experimentele .nupkg. Geen NuGet.org-publicatie of ondersteunde release; licentiekeuze blijft open en licenties van derden blijven gelden.

## Aparte testapp configureren
Gebruik een Logic Apps Standard .NET 8 in-process-testapp op Functions runtime v4. Stel FUNCTIONS_WORKER_RUNTIME=dotnet en FUNCTIONS_INPROC_NET8_ENABLED=1 in. Configureer storage en managed identity. Het voorbeeld is NuGet-based en heeft geen extensionBundle in host.json; combineer niet met een andere bundelversie. Behoud storage/VNet-instellingen van de app.

connections.json gebruikt logische connection apim en verwijst naar APIM_CONNECTION_PROFILE. Stel die app setting als JSON in:

```json
{
  "subscriptionId": "YOUR_SUBSCRIPTION_GUID",
  "resourceGroup": "YOUR_TEST_RESOURCE_GROUP",
  "serviceName": "YOUR_TEST_APIM",
  "developerTokenSetting": "APIM_DISCOVERY_TOKEN_DEVELOPER",
  "gatewayBaseUrl": "https://YOUR_GATEWAY_HOSTNAME",
  "audience": "api://YOUR_BUSINESS_API_APPLICATION",
  "subscriptionKeySetting": "APIM_SUBSCRIPTION_KEY"
}
```

subscriptionKeySetting is optioneel. Gebruik Key Vault-references voor keys. Businessidentiteit vereist Entra-autorisatie op de API; APIM-ARM-RBAC alleen volstaat niet.

Stel APIM_DISCOVERY_TOKEN_DEVELOPER in op een kortlevend ARM-token van de bedoelde developer. Commit het token nooit en sla het niet in de workflow op. Een hosttoken is een handmatig PoC-mechanisme, geen interactieve login, impersonatie van de Portalgebruiker of veilige scheiding tussen developers. Gebruik dit niet als productie-isolatie. Andere gebruikers mogen geen gedeeld profiel met hogere rechten erven. Productie-delegatieautorisatie staat open.

Kopieer voor lokale ontwikkeling local.settings.example.json naar local.settings.json en vul alleen lokale waarden in. Start Azurite voor de voorbeeldstorage en Functions Core Tools v4. Lokale credentials/settings zijn van publish uitgesloten. Lokale runtimestart is hier nog niet uitgevoerd.

## Azure Portal-test
1. Controleer what-if voor de fictieve Orders-fixture op een aparte test-APIM.
2. Deploy uitsluitend de gepubliceerde voorbeeldoutput naar een aparte Standard-testapp: zip-deploy naar approot, niet de bovenliggende map. Voorbeeld: az webapp deployment source config-zip --resource-group YOUR_TEST_GROUP --name YOUR_TEST_APP --src YOUR_PUBLISH_ZIP. Dit is niet tegen Azure uitgevoerd; zip-deploy vervangt appcontent, gebruik een aparte testapp.
3. Herstart, controleer startupregistratie, open GetOrder en voeg API Management / Invoke an API operation toe.
4. Kies connection/profiel, API-familie, versie en operatie. Controleer orderId en versiegebonden responsevelden.
5. Sla op/heropen en controleer dat payload._binding bewaard blijft. Stop bij ontbreken: runtime geeft bewust BindingSnapshotMissing zonder ARM-fallback.
6. Verbind orderId met requesttrigger en gebruik body.id in een responseactie. Beveilig gevoelige inputs/outputs en controleer echte run history.
7. Voer uit, trek discovery-ARM-toegang in en voer de opgeslagen workflow opnieuw uit; businessuitvoering moet blijven werken.
8. Voeg een API/versie toe en vernieuw zonder extensionbuild. Wijzig familiekeuze met oude vervolgwaarden en controleer afwijzing/invalidation.
9. Verifieer developerrechten, managed identity, retries en DNS/routing. Leg resultaten en versies vast; cloudcriteria blijven open tot bewezen.

## Netwerk, upgrade en rollback
APIM is door de gebruiker bevestigd als VNet-geïntegreerd met dezelfde intern/publiek bereikbare DNS-naam. Gebruik die naam in gatewayBaseUrl per omgeving; DNS/routing van Standard bepaalt de route. ARM-discovery en tijdelijke exportstorage vragen aparte bereikbaarheid. Vervang de HTTPS-hostnaam niet door een privé-IP.

Promoveer dezelfde workflow met alleen omgevingssettings na controle van API-/versionsetnamen, paden en contracten. Bewaar workflow/configuratie en vorige deploymentpackage vóór upgrades. Rollback is herdeployment van vorige package met compatibele settings. Verwijder connectoractieverwijzingen vóór de extension; behoud overige settings/connections. Geef een bestaande release nooit stilzwijgend een ander contract.
