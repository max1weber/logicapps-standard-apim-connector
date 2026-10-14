# Installatie

[English](../en/installation.md)

Revisie: 2026-10-14. Handleiding in ontwerpfase; er bestaat nog geen connectorrelease.

## Repository voorbereiden

Clone de repository met Git en open deze in je ontwikkelagent. Matt Pocock-skills staan met ondersteunende bestanden onder .agents/skills; ze zijn niet globaal geïnstalleerd. Zie [agent-skills](agent-skills.md).

## Connectorinstallatie: release-eisen

Uitvoerbare commando’s volgen wanneer de PoC SDK/runtimecompatibiliteit heeft vastgesteld. De gevalideerde procedure behandelt:

1. Standard-runtime, OS/hosting, .NET, Functions Core Tools en VS Code-extensionversies.
2. Installatie/registratie in nieuwe en bestaande projecten, packaging en Azure-deployment.
3. Deployment van een aparte discoveryhost, indien gekozen.
4. Gescopeerde APIM-lees/exportrechten en geauthenticeerde designertoegang.
5. Runtime managed identity, business-audience en optionele subscription key via Key Vault.
6. Connections en app settings per omgeving.
7. Private APIM-DNS, VNet-integratie en routing.
8. Lokale developerauthenticatie en discoveryvernieuwing.
9. Verificatie in VS Code en Azure Portal: API/versie/operatie selecteren, getypeerde velden controleren en aanroepen.
10. Dezelfde workflow van DEV naar TST promoveren; alleen omgevingsconfiguratie wijzigen.
11. Upgrade, rollback, verwijderen en troubleshooting.

## Acceptatie

Doorloop de handleiding in een schoon Standard-project. Ontdek nieuwe API’s/versies zonder extensionbuild. Voer opgeslagen operaties uit met uitgeschakelde managementtoegang voor discovery. Controleer retries, timeouts en beveiligde run history. Leg designerbeperkingen vóór release vast; installatie is nog niet gevalideerd.
