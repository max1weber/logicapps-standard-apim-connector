# Repository instructions / Repository-instructies

## English
Build a C#/.NET Logic Apps Standard connector using vertical slices. Live design-time discovery and typed schemas are mandatory PoC gates; do not claim unverified designer support. Runtime business calls must not need management-plane access. Keep documentation in English and Dutch.

## Nederlands
Bouw een C#/.NET Logic Apps Standard-connector met vertical slices. Live designtime-discovery en getypeerde schemas zijn verplichte PoC-criteria; claim geen onbewezen designerondersteuning. Runtime-businessaanroepen vereisen geen managementrechten. Houd documentatie Engels en Nederlands.

## Agent skills

### Issue tracker
GitHub Issues in max1weber/logicapps-standard-apim-connector. See / zie [issue tracker](docs/agents/issue-tracker.md).

### Triage labels
Standard roles / Standaardrollen: needs-triage, needs-info, ready-for-agent, ready-for-human, wontfix. See / zie [triage labels](docs/agents/triage-labels.md).

### Domain docs
Single-context: root GLOSSARY.md and docs/adr/. See / zie [domain docs](docs/agents/domain.md).

Read the relevant installed SKILL.md before invoking a skill. / Lees de relevante geïnstalleerde SKILL.md vóór gebruik.

## Branch workflow / Branchafspraken
Always fetch the latest develop and create a fresh feature branch from that commit. Target develop for PRs. Merge only after CI for the current head is green and blocking review findings are resolved.

Haal altijd de actuele develop op en maak daarop een verse featurebranch. Richt PR’s op develop. Merge pas bij groene CI voor de actuele head en opgeloste blokkerende reviewbevindingen.
