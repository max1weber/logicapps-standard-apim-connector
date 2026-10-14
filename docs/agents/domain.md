# Domain Docs

How the engineering skills should consume this repo's domain documentation when exploring the codebase.

## Before exploring, read these

- **`GLOSSARY.md`** at the repo root, or
- **`GLOSSARY-MAP.md`** at the repo root if it exists: it points at one `GLOSSARY.md` per context. Read each one relevant to the topic.
- **`docs/adr/`**: read ADRs that touch the area you're about to work in. In multi-context repos, also check `src/<context>/docs/adr/` for context-scoped decisions.

If any of these files don't exist, **proceed silently**. Don't flag their absence; don't suggest creating them upfront. The `/domain-modeling` skill (reached via `/grill-with-docs` and `/improve-codebase-architecture`) creates them lazily when terms or decisions actually get resolved.

## File structure

Single-context repo (most repos):

```
/
├── GLOSSARY.md
├── docs/adr/
│   ├── 0001-event-sourced-orders.md
│   └── 0002-postgres-for-write-model.md
└── src/
```


## Vocabulary and decisions
Use glossary terms consistently. Flag contradictions with existing ADRs explicitly. Create domain documents lazily as concepts and decisions are resolved. Maintain English and Dutch sections in the canonical GLOSSARY.md and ADR files.

## Nederlands
Dit is één domeincontext. Lees vóór codeverkenning GLOSSARY.md in de root en de relevante ADR’s onder docs/adr/. Ontbreken die documenten, ga dan verder zonder te blokkeren of direct aanmaak voor te stellen. Domeinmodellering maakt ze aan zodra begrippen of beslissingen worden vastgesteld.

Gebruik vastgelegde glossarytermen in issues, tests en ontwerpen; signaleer echte hiaten voor domeinmodellering. Meld conflicten met bestaande ADR’s expliciet. Houd de canonieke glossary en ADR’s tweetalig. Het project gebruikt geen GLOSSARY-MAP.md of meerdere contexten.
