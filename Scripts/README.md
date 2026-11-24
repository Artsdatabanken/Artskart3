# Scripts

Operative skript som kjøres for hånd. Ingenting her er del av release-pipelinen,
med ett unntak: `BackfillAll.sql` kjøres som migrasjon (se under).

Kolonnen **Skriver** er det viktigste i tabellen. Alt som bare leser er trygt å
kjøre når som helst; alt som skriver skal avklares først, og mot hvilken instans.

| Skript | Gjør | Skriver | Når |
|---|---|---|---|
| `BackfillAll.sql` | Autoritativ datafylling av `ObservationEntityIndex` og de denormaliserte filterkolonnene | **Ja** | Kjøres av migrasjon — ikke for hånd |
| `BuildAreaCountCache.sql` | Bygger områdebufferen (`AreaCountCacheLevel1/2`) | **Ja** | Etter endring i dimensjonsregisteret, eller når bufferen er stale |
| `BuildLocationCountCache.sql` | Bygger lokasjonsbufferen (`LocationCountCacheLevel1`) | **Ja** | Samme som over, men **alltid etter** `BuildAreaCountCache.sql` — den låner bøttene derfra og nekter å bygge på foreldede. ~6 min over 134 mill. kilderader |
| `BackfillOceanAndSvalbardAreas.sql` | Målrettet reparasjon av indeksen for områdetype 4 og 6 | **Ja** | Engangsverktøy — se *Reparasjon* under |
| `RebuildLocationAreasForAreaTypes.sql` | Regner `LocationAreas` på nytt ved romlig snitt | **Ja** | Engangsverktøy — se *Reparasjon* under |
| `VerifyLocationCountCache.sql` | Avstemmer lokasjonsbufferen mot tellingen, lokasjon for lokasjon | Nei | Etter hver bygging av bufferen |
| `CheckAreaIndexDrift.sql` | Avstemmer `ObservationEntityIndex` mot `Area` | Nei | Etter hver `Area`-oppdatering — se *Drift* under |
| `CheckAreaHierarchyInvariant.sql` | Kontrollerer at hver kommunerad har fylkesraden for sitt fylke | Nei | Fra pipelinen, og etter hvert skript som skriver til indeksen — se *Invarianten* under |
| `IndexUsageDiff.sql` | Indeksbruk før/etter en endring | Nei | Ved indeksarbeid |
| `Artskart2NodeDatasets.sql` | Lager `artskart2-nodes.json`, tabellen som oversetter importkilder i gamle Artskart 2-lenker | Nei | Etter at databasen er bygd på nytt — se *Artskart 2-lenker* under |
| `CheckArtskart2Links.sql` | Kontrollerer det gamle Artskart 2-lenker trenger fra databasen | Nei | Samme som over |
| `MeasureLocationSortCost.sql` | Bryter ned hva et buffret lokasjonsoppslag bruker tiden på | Nei | Ved regresjon i lokasjonsstien |
| `PerfTestListView.ps1` | Kort måling av listevisningen | Nei* | Rask sjekk |
| `PerfTestAreaCounts.ps1` | Kort måling av områdetellingene | Nei* | Rask sjekk |
| `PerfSuite/` | Hele ytelsessuiten | Nei* | Se `PerfSuite/README.md` |

\* Skriver ikke, men krever et kjørende API mot en produksjonslignende database
og legger last på den. Spør først. `PerfSuite/Run.ps1` tar fra titalls minutter
til flere timer og skal ikke startes uten avtale.

## BackfillAll er ikke et løst skript

Filen er bygget inn som `EmbeddedResource` i `Artskart3.Infrastructure.csproj` og
kjøres av migrasjon `20260828065007_BackfillAll`. Den ligger her, og ikke inne i
migrasjonen, nettopp for at det skal være samme SQL man kan kjøre manuelt i SSMS.

**Flytt eller slett den ikke.** Forsvinner `EmbeddedResource`-oppføringen,
feiler migrasjonen med en melding som peker tilbake hit.

## Bufferne og @SchemaVersion

`BuildAreaCountCache.sql` og `BuildLocationCountCache.sql` har hver sin
`DECLARE @SchemaVersion INT`, og verdien er pinnet tre steder som må være like:

- `AreaCountCacheDimensions.SchemaVersion` (FNV-1a over dimensjonsregisteret)
- `@SchemaVersion` i **begge** skriptene
- `SchemaVersion` i `LocationCountCacheState` / `AreaCountCacheState`

Stemmer de ikke, nekter oppslaget å bruke bufferen og faller tilbake til
aggregering. Det er riktig oppførsel, men den er stille: svarene blir like
riktige, bare tregere, og det eneste sporet er en `LogWarning`.

Endres dimensjonsregisteret, feiler
`AreaCountCacheDimensionsTests.Skjemaversjonen_er_deterministisk`. Da skal begge
skriptene oppdateres **og** begge bufferne bygges på nytt.

## Drift mot Area

`BackfillAll` seksjon A setter bare inn — det finnes ingen `DELETE` i skriptet.
Endrer `Area` seg (nye Fid-er, gamle satt til `IsCurrent = 0`, justerte grenser),
blir gamle indeksrader liggende, og en observasjon telles i både gammelt og nytt
område.

Ufiltrerte karttall kommer fra `Area.ObservationCount` og ser riktige ut.
Filtrerte tall kommer fra indekstabellen og blir for høye. Kjør derfor
`CheckAreaIndexDrift.sql` etter hver `Area`-oppdatering — avviket må måles, ikke
antas.

## Invarianten områdetellingene hviler på

`AreaCountGeoJoinRules.CanSkipGeoJoin` utelater geo-semijoinen når utdataområdene
er kommuner i et valgt fylke. Det forutsetter at hver kommunerad i indeksen
følges av fylkesraden for kommunens eget fylke. Holder det ikke, blir
områdetallene **for høye uten feilmelding**.

`CheckAreaHierarchyInvariant.sql` kontrollerer det, leser bare, og stopper med
severity 16 ved brudd. Kjør den fra pipelinen, etter hvert skript som skriver til
`ObservationEntityIndex`, og før tellebufferne bygges — bygges de over en brutt
invariant, stivner de gale tallene i `AreaCountCacheLevel1`.

Samme kontroll står også til slutt i `BackfillAll.sql`, men den filen er embeddet
i en migrasjon og kjører derfor bare på ferske databaser. I produksjon er det
dette skriptet som gjelder.

## Reparasjon etter en Area-oppdatering

`LocationAreas` er kilden indeksen utledes fra
(`Observation` → `Location` → `LocationAreas` → `Area`), og **ingenting i dette
repoet fyller den** — den kommer fra en prosess utenfor løsningen. Oppdateres
`Area` uten at `LocationAreas` følger med, kan backfillen ikke utlede rader for
de nye områdene, og siden den også sletter rader som ikke lar seg utlede, tømmes
indeksen for de typene i stedet for å fylles.

De to skriptene er ett forløp, i denne rekkefølgen:

1. `RebuildLocationAreasForAreaTypes.sql` — reparerer `LocationAreas`
2. `BackfillOceanAndSvalbardAreas.sql` — reparerer indeksen for type 4 og 6

Å reparere indeksen alene holder ikke: neste backfill sletter radene igjen, fordi
kilden fortsatt er tom. Begge er skrevet for en konkret hendelse, men formen
gjentar seg ved hver `Area`-oppdatering som ikke følges av `LocationAreas`.

## Artskart 2-lenker etter at databasen er bygd på nytt

Gamle Artskart 2-lenker (#733) oversettes i frontend, men tre ting henter de fra
databasen: bokmerkene i `Filter`, Id-er som må være de samme som i Artskart 2
produksjon, og filterkolonnene som fylles fra taggene. Etter en ny kopi:

1. Kjør `Artskart2NodeDatasets.sql` og lagre resultatet over
   `Artskart3.WebApp/src/assets/artskart2-nodes.json`. Hvilke importkilder som kan
   oversettes til datasett, avhenger av dataene, så tabellen må lages på nytt.
2. Kjør `CheckArtskart2Links.sql`. Hver spørring sier hva den forventer.
3. Start API-et og frontend mot den nye databasen, og kjør fra `Artskart3.WebApp`:

   ```bash
   node --no-warnings scripts/check-artskart2-links.mjs --year-to <siste hele år før kopien>
   ```

   Skriptet sjekker områdetabellen (`artskart2-areas.json`) mot områdene i
   Artskart 3, at observasjoner, organisasjoner og bokmerker har samme Id som i
   Artskart 2 produksjon, og sammenligner antall treff for rundt 40 oversatte
   filtre med Artskart 2 produksjon. Det sender omtrent 40 telleforespørsler til
   hver av dem og tar et par minutter. Kjente avvik, som koordinatpresisjon uten
   verdi, står forklart i utskriften og regnes ikke som feil.

`artskart2-areas.json` lages fra Artskart 2 produksjon med
`scripts/generate-artskart2-areas.mjs` og trenger ikke lages på nytt så lenge
Artskart 2 ikke endrer områdene sine. Steg 3 sjekker at kodene den peker på,
fortsatt finnes i Artskart 3.
