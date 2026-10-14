# Epic-concept

[English](../en/epic.md)

### Resultaat en gebruikersverhaal
Als Logic Apps-developer wil ik live een APIM-API, versie en operatie selecteren in de designer, met getypeerde invoer/uitvoer, zodat dezelfde workflow zonder inhoudelijke wijziging door DEV/TST/ACC/PRD kan gaan. Nieuwe API’s verschijnen na vernieuwen zonder connectorbuild. Runtime gebruikt de gatewayconfiguratie van de doelomgeving en heeft voor businessaanroepen geen managementrechten nodig.

### Oplevering
- [ ] Afhankelijke keuzelijsten en dynamische schemas bewijzen in VS Code en Azure Portal.
- [ ] Ondersteunde SDK/runtime en designer-authenticatie vaststellen.
- [ ] Live discovery met paginering, versionsets, onversioneerde API’s en vernieuwen.
- [ ] Discovery-leesrechten scheiden van business-API-authenticatie.
- [ ] Stabiele bindings en contractsnapshots zonder omgevingshostname opslaan.
- [ ] REST/JSON-runtime met managed identity, optionele subscription keys en pad/query/headerversies.
- [ ] Retry, timeout, runAfter, foutafhandeling en beveiligde run history toetsen.
- [ ] Dezelfde workflow in twee omgevingen bewijzen.
- [ ] Vertical slices met eigen handlers, validatie en tests gebruiken.
- [ ] Volledige Engelse/Nederlandse documentatie en installatie in een schoon project.
- [ ] Reproduceerbare releases met compatibiliteitsmatrix en changelog.
- [ ] Licentie besluiten: eigenaar wil verbeteringen terug; bestaande MIT verplicht dit niet.
- [ ] Matt Pocock-repositorysetup afronden na review van geïnstalleerde skills.

Gereed wanneer nieuwe API’s zonder rebuild zichtbaar zijn, schemas getypeerd zijn, runtime zonder managementtoegang werkt, installatie uitsluitend met de handleiding lukt en documentatieparen overeenkomen. Bij onvoldoende designerondersteuning eerst beperking bewijzen en een expliciete productkeuze maken.

