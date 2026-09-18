# Instruksjoner for AI-agenter i Artskart3

## Kommentarer

Alle nye kodekommentarer i denne løsningen skal skrives på norsk.

## Skript som ikke skal kjøres uendret

### `Scripts/PerfSuite/Run.ps1` — krever eksplisitt beskjed

**Ikke start dette skriptet med mindre brukeren uttrykkelig har bedt om at det
kjøres nå.** Det er en langtkjørende ytelsessuite: én kjøring tar fra titalls
minutter til flere timer, sender tusenvis av spørringer og legger merkbar last på
databasen. At en oppgave handler om ytelse er ikke i seg selv en grunn til å
starte den.

Dette er trygt uten å spørre:

- `Scripts/PerfSuite/Run.ps1 -ListOnly` — skriver ut testmatrisen, gjør ingen
  målinger og rører ikke databasen. Krever at `fixtures.json` allerede finnes.
- `Scripts/PerfSuite/Compare.ps1` — leser bare CSV-filer fra tidligere kjøringer.
- `Scripts/PerfSuite/Test-Matrix.ps1` — selvtest av matrisegenereringen. Kjør
  denne etter endringer i `Fixtures.ps1` eller `Cases.ps1`.

Trenger du en rask måling, bruk de kortere `Scripts/PerfTestListView.ps1` eller
`Scripts/PerfTestAreaCounts.ps1` — men spør først, siden også de krever et
kjørende API mot en produksjonslignende database.

Se `Scripts/PerfSuite/README.md` for detaljer.

## Databasetilgang

Å trenge fakta fra databasen er ikke i seg selv tillatelse til å koble seg til
den. Spør først, og spør hvilken instans.
