# Ytelsessuite

> ## ⛔ Ikke kjør `Run.ps1` uten at det er avtalt
>
> **Til agenter og automatikk:** `Run.ps1` skal ikke startes uten at brukeren
> eksplisitt har bedt om det. En kjøring tar fra titalls minutter til flere timer,
> sender tusenvis av spørringer og legger merkbar last på databasen.
>
> Skal du bare undersøke hva suiten dekker, bruk `Run.ps1 -ListOnly` — den gjør
> ingen målinger. `Compare.ps1` leser bare CSV-filer, og `Test-Matrix.ps1` er en
> selvtest av matrisen — begge er også trygge.
>
> `-ListOnly` krever at `fixtures.json` allerede finnes, siden testdataene må
> hentes fra API-et én gang. Før den første kjøringen er `Test-Matrix.ps1` det
> som viser matrisens form og størrelse.
>
> For raske, avgrensede målinger finnes `Scripts/PerfTestListView.ps1` og
> `Scripts/PerfTestAreaCounts.ps1`.

## Hva den er til

To ting:

1. **Finne trege filterkombinasjoner** — ikke bare de enkeltfiltrene vi allerede
   mistenker. Vi har 17 filterdimensjoner i `IObservationFilter`, som gir 136 par
   og over 600 tripler. De eldre skriptene dekket rundt åtte kombinasjoner,
   håndplukket, så de kunne bare bekrefte det vi allerede trodde.
2. **Oppdage regresjoner.** Resultatene lagres per kjøring, og `Compare.ps1`
   stiller to kjøringer mot hverandre.

## Filer

| Fil | Rolle |
|---|---|
| `Run.ps1` | Selve suiten. **Kjøres kun manuelt, på eksplisitt beskjed.** |
| `Compare.ps1` | Sammenligner to resultatfiler. Rører ikke API-et. |
| `Test-Matrix.ps1` | Selvtest av matrisegenereringen. Rører verken API eller database. |
| `PerfLib.psm1` | Delt bibliotek: API-kall, måling, sammenligning, analyse. |
| `Fixtures.ps1` | Oppdagelse og lagring av testdata. |
| `Cases.ps1` | Genererer testmatrisen. |
| `fixtures.json` | Frosne testdata. Generert, ikke i git. |
| `results/*.csv` | Måleresultater. Generert, ikke i git. |

## Kom i gang

API-et må kjøre mot en database med produksjonslignende datamengder — en tom
database gir misvisende tall. Vi bruker `Artskart3IndexProdLikeTestMigrations`:

```json
"ConnectionStrings:DefaultConnection": "data source=localhost;initial catalog=Artskart3IndexProdLikeTestMigrations;Integrated Security=true;MultipleActiveResultSets=True;TrustServerCertificate=True"
```

```powershell
# Se hva matrisen dekker uten å måle noe
./Scripts/PerfSuite/Run.ps1 -ListOnly

# Første ekte kjøring. Bygger fixtures.json underveis.
./Scripts/PerfSuite/Run.ps1 -Level Standard

# Senere: sammenlign mot forrige
./Scripts/PerfSuite/Run.ps1 -Level Standard -Compare Scripts/PerfSuite/results/<forrige>.csv

# Eller sammenlign to eksisterende filer uten å kjøre noe
./Scripts/PerfSuite/Compare.ps1
```

## Nivåer

Nivåene er en trapp, tenkt brukt slik: **`Quick`** for en kjapp bekreftelse på at
en ytelsesendring virker, **`Standard`** hvis det ser bra ut, **`Full`** når noe
skal dokumenteres eller en baselinje settes.

| `-Level` | Innhold | Målinger | Tid |
|---|---|---|---|
| `Quick` | Enkeltfiltre + oppslag. 6 lokasjonsutsnitt, ett fra hvert tetthetsnivå, med 6 faste filtre | 266 | **~3 min** |
| `Standard` | + alle dimensjonspar på tunge nivåer, 60 tripler, 30 kvadrupler, brukerreiser. Alle 16 lokasjonsutsnitt; `oslo` får hele `enkelt`-laget, resten de 10 faste | 1 475 | **~27 min** |
| `Full` | + par også på lette nivåer, 300 tripler, 120 kvadrupler. `oslo` får hele matrisen | 6 758 | **~2,6 t** |

Tidene er målt, ikke gjettet: 330 ekte kall mot
`Artskart3IndexProdLikeTestMigrations`, med egne prøver for hver av de tunge
endepunktvariantene. De vil variere med maskin og datamengde.

Er du usikker på hvor lang tid det tar hos deg, start med
`-Level Full -TimeBudgetMinutes 30`. Suiten stopper pent når budsjettet er brukt
opp, og resultatene er allerede skrevet til disk.

## Hvordan matrisen bygges

**En dimensjon** er ett filterfelt (kommune, takson, periode …).
**Et nivå** er én konkret verdi for den dimensjonen, med en rolle:

- `tung` — lav selektivitet, mange treff
- `lett` — høy selektivitet, få treff
- `annen` — varianter vi vil dekke, men ikke bruke i parkombinasjonene

Begge ytterpunktene trengs. Områdetellingene er tregest for **tunge** filtre, der
det er mye å aggregere. Listevisningen er tregest for **lette**, fordi `TOP` må
lese langt ned i klyngeindeksen før den er fylt. Måler man bare det ene nivået,
ser man bare halvparten av problemene.

Casene genereres i lag som kjøres i rekkefølge, slik at en avbrutt kjøring
fortsatt er brukbar:

| Lag | Innhold |
|---|---|
| `enkelt` | Hvert nivå av hver dimensjon alene, pluss referansen uten filter |
| `par` | Alle dimensjonspar |
| `trippel` | Deterministisk utvalg av tre dimensjoner |
| `kvadruppel` | Deterministisk utvalg av fire |
| `reise` | Håndplukkede, realistiske brukerflyter med 3–8 filtre |
| `oppslag` | Typeahead-endepunktene, som ikke tar filterkropp |

Hver case kjøres mot flere endepunkter (`Observation` side 1 og side 50,
`AreaCounts` z1/z2, `Locations` og `LocationPolygons` med 16 kartutsnitt).
Et endepunkt kan snevre inn hvilke case det gjelder for, med `Layers` eller
`CaseKeys` — se `Test-PerfEndpointRunsCase`. `AreaMarkers` bruker det til å kjøre
bare for referansen uten filter; lokasjonsutsnittene til å slippe å gjenta hele
filtermatrisen 16 ganger.

Lagene deler seg også etter hvilken kodesti de treffer. Målt på `Full`:

| Sti | Case |
|---|---|
| Områdebuffer (1–2 bufrbare filtre) | 220 |
| Database — blokkert filter | 419 |
| Database — tre eller flere filtre | 142 |

«Blokkert» betyr at filteret ikke kan besvares fra områdebufferen uansett hvor få
filtre som er satt. Det gjelder `kommune`, `fylke`, `havomraade`, `katalognr` og
takson under ordensnivå. Den største databasegruppen er altså ikke triplene, men
blokkerte filtre på ett og to nivå.

## Hvorfor testdataene er frosset

Suiten skal svare på «ble noe tregere siden sist». Da må casene være de samme
mellom kjøringer. Slår vi opp «tyngste institusjon» på nytt hver gang, kan den
bytte identitet når data vokser, og sammenligningen måler noe helt annet enn den
tror.

Derfor skrives det oppdagede utvalget til `fixtures.json` og gjenbrukes.
`-RefreshFixtures` bygger dem på nytt — gjør det sjelden, og forkast gjerne gamle
baselinjer samtidig.

Av samme grunn er utvalget av tripler deterministisk (`-Seed`). Endrer du seeden,
endrer du matrisen, og sammenligningen mot gamle kjøringer mister de casene som
falt ut.

## AreaCounts og AreaMarkers

`AreaCounts` er metrikken for tellestien. Den kjører samme
`FilterAndComputeCounts` som `AreaMarkers`, men uten geometri — altså uten
polygonlasting, WKT-konvertering og 21 MB serialisering oppå det vi vil måle.

Endepunktet har fem minutters minnecache per filter i `SearchService`, så bare
**første** kall per filter måler databasen. Derfor kjører suiten det med
`Runs = 1`. Hvert case har sitt eget filter, så første kall er alltid kaldt, og
`Measure-PerfRequest` faller tilbake til den kalde tiden når det ikke finnes varme
kjøringer. Både `ColdMs` og `WarmMs` blir dermed det kalde tallet, og `Compare.ps1`
sammenligner riktig uten særbehandling.

`AreaMarkers` kjøres **bare ufiltrert**. Frontend henter det nøyaktig to ganger per
sidelast, ett kall per zoomnivå (`prefetchAreaGeometries`), og legger geometrien i
en klientside cache som aldri tømmes. Alle senere filterendringer går til
`AreaCounts`. Filtrert `AreaMarkers` forekommer ikke, og 1012 slike målinger ble
fjernet fra suiten.

## Lokasjonsutsnittene

Frontend henter lokasjoner først fra OpenLayers-zoom 11 (`ZoomConfig`,
`ApiZoomLevel.LocationPoints`). Det største utsnittet endepunktet noen gang får
er dermed **185 × 103 km**, målt i kartet.

De gamle utsnittene lå over det taket: `norge` var 1200 × 1510 km og `trondelag`
200 × 200 km. Verre var at områdefixturene ikke lå i nærheten av `oslo`-utsnittet
på 40 km — Farsund, Trøndelag, Nordland og Troms ga alle **null** lokasjoner der.
419 av 777 oslo-case målte altså et tomt svar, og `fylke:tung` brukte 2005 ms på
å returnere ingenting.

16 utsnitt i to grupper:

Tallene er hvor mange lokasjoner som **finnes** i utsnittet, ikke hvor mange
endepunktet returnerer. `MaxResults` er 100 000, så alt over det kuttes.

| Gruppe | Utsnitt | Lokasjoner | Returnerer |
|---|---|---|---|
| store, zoom 11-takstørrelse (185 × 103 km) | `oslo` | 967 649 | 100 000 |
| | `trondheim` | 274 481 | 100 000 |
| | `trollheimen` (148 × 83 km) | 158 550 | 100 000 |
| store, under `MaxResults` (80 × 80 km) | `kristiansand` | 90 278 | 90 278 |
| stige ~50 000 | `telemark50k`, `innlandet50k`, `ostfold50k` | 45–50 000 | alt |
| stige ~10 000 | `salten10k`, `ostfold10k`, `valdres10k` | ~10 000 | alt |
| stige ~1 000 | `gudbrand1k`, `varanger1k`, `agder1k` | ~1 000 | alt |
| stige ~100 | `mjosa100`, `namdal100`, `telemark100` | ~100 | alt |

`kristiansand` er det eneste store utsnittet som ikke kuttes. De tre andre
returnerer 100 000 rader uansett hvor mange lokasjoner de inneholder, så
forskjellen mellom dem er aggregeringsarbeid, ikke svarstørrelse. Uten et stort
utsnitt under taket ville vi aldri målt tilfellet der `TOP`-en må rangere alle
gruppene i stedet for bare å kutte — og en endring i `MaxResults` ville vist seg
først der.

Alle er disjunkte, så ingen måling kan lese data en tidligere måling nettopp
varmet opp. Eneste unntak er `trollheimen`/`trondheim`, som overlapper fordi
begge er hentet rett fra kartet.

Utsnittene er ikke likeverdige. 16 utsnitt × 2 endepunkter × 777 case ville gitt
24 864 målinger — over fire ganger hele kjøringen, for to endepunkter. Derfor har
hvert utsnitt en rolle, og rollen endrer seg med nivået. Tallene er case per
endepunkt; ganger to, siden `Locations` og `LocationPolygons` kjører likt.

| Utsnitt | Lokasjoner | `Quick` | `Standard` | `Full` |
|---|---|---|---|---|
| `oslo` | 967 649 | 6 faste | `enkelt` (93) | alle (777) |
| `trondheim` | 274 481 | — | 10 faste | `enkelt` + `par` (349) |
| `trollheimen` | 158 550 | — | 10 faste | `enkelt` (93) |
| `kristiansand` | 90 278 | 6 faste | 10 faste | `enkelt` (93) |
| `telemark50k` | 49 939 | 6 faste | 10 faste | 10 faste |
| `innlandet50k`, `ostfold50k` | ~46 000 | — | 10 faste | 10 faste |
| `salten10k` | 10 046 | 6 faste | 10 faste | 10 faste |
| `ostfold10k`, `valdres10k` | ~10 000 | — | 10 faste | 10 faste |
| `gudbrand1k` | 1 001 | 6 faste | 10 faste | 10 faste |
| `varanger1k`, `agder1k` | 1 000 | — | 10 faste | 10 faste |
| `mjosa100` | 101 | 6 faste | 10 faste | 10 faste |
| `namdal100`, `telemark100` | 100 | — | 10 faste | 10 faste |

`Quick` tar **ett utsnitt fra hvert tetthetsnivå** — over taket, under taket,
50 000, 10 000, 1 000 og 100. En forbedring som bare slår inn på store svar,
eller bare på små, skal synes med én gang. De seks faste casene er referansen,
det verste områdefilteret (`fylke:tung` gir en OR mellom områdetype 2 og 6 som
columnstore ikke kan pushe ned), to brede listefiltre, taksonhierarkiet, og ett
selektivt som fanger regresjoner i den raske stien.

`Standard` tar alle 16 utsnitt, men bare `oslo` får hele `enkelt`-laget. Stigen
skal svare på hvordan endepunktet skalerer med antall lokasjoner — der er 777
filtervarianter per utsnitt støy, ikke signal.

Før dette kjørte `Quick` ingen lokasjoner i det hele tatt, og `Standard` hoppet
rett til de fire tyngste utsnittene.

`LocationPolygons` kjører på nøyaktig samme utsnitt og samme case som
`Locations`. Ellers kan ikke en regresjon plasseres — ligger den i lokasjons-
søket eller i polygonhentingen?

Kartutsnittene **fryses ikke** i `fixtures.json`, i motsetning til resten.
Frysingen finnes fordi oppdagede data kan bytte identitet når databasen vokser.
Utsnittene er faste konstanter i `Fixtures.ps1`, og leses de fra den lagrede
JSON-en i stedet, blir en endring stille ignorert til noen kjører
`-RefreshFixtures` — som samtidig forkaster alle baselinjer.

## Indekssporing

```powershell
./Scripts/PerfSuite/Run.ps1 -Level Full `
    -SqlServer localhost -SqlDatabase Artskart3IndexProdLikeTestMigrations
```

Med begge flaggene satt leser suiten `sys.dm_db_index_usage_stats` før og etter
hver måling og skriver differansen til kolonnen `Indexes` i CSV-en, på formen
`IX_Observation_ListView:c1;PK_ObservationEntityIndex:s40`. Lesningen skjer etter
at stoppeklokken er stoppet, så den havner aldri i måletallet.

Det skiller to funn som ser helt like ut i en ren tidssammenligning:

| Observasjon | Betydning |
|---|---|
| Samme indeks, lengre tid | Mer data, eller en maskin under last |
| **Annen indeks**, lengre tid | Planvipping |

Bare det andre er verdt en alarm. `Compare.ps1` viser det i kolonnen `Indeks`
og oppsummerer de hyppigste planendringene under regresjonstabellen.

Dette ble lagt inn etter kjøringen 11.09.2026, der `IX_Observation_ListView` ga
142 forbedringer og 202 regresjoner. Årsaken — at regresjonene hadde byttet til
den nye indeksen mens forbedringene også brukte den, og at forskjellen var om
filteret var tett eller spredt — krevde to fulle kjøringer og en manuell
plananalyse å finne. Med denne kolonnen ville det stått i rapporten.

Forbehold:

- Tellerne nullstilles ved omstart av SQL Server og ved indeksgjenoppbygging.
  Suiten leser alltid utgangsverdien, men en nullstilling *midt i* en kjøring gir
  negative differanser, som hoppes over.
- Den sier hvilken indeks, ikke hvorfor. Til planen trengs Query Store.
- Databasen må være den samme API-et peker på — ellers måles en annen instans
  enn den som svarer.

## Å lese rapporten

- **Tregeste case** — den vanlige topplista, sortert på varm tid.
- **Per endepunkt / per lag** — hvor kostnaden ligger på grovt nivå.
- **Marginalkostnad per dimensjon** — snitt for case *med* dimensjonen minus
  snitt for case *uten*. Dette er analysen enkeltmålinger ikke kan gi: med et par
  hundre kombinasjoner kan vi peke ut hvilken dimensjon som faktisk koster.
  Merk at det er en korrelasjon, ikke et bevis — dimensjoner som ofte opptrer
  sammen får liknende skår. Bruk den til å velge hva som skal undersøkes
  nærmere.
- **Kald vs. varm tid** — kald er første kjøring (plankompilering, lesing fra
  disk), varm er medianen av resten. Stort sprik betyr at spørringen leser mye
  data.

Kjør `Scripts/IndexUsageDiff.sql` før og etter for å se hvilke indekser som
faktisk ble brukt.

## Etter endringer i suiten

Kjør `./Scripts/PerfSuite/Test-Matrix.ps1`. Den bygger syntetiske fixtures og
kontrollerer at matrisen blir riktig — uten å røre API eller database.

Testen finnes fordi to reelle feil slapp gjennom under utviklingen, og begge
ville gitt målinger som så helt fine ut:

- `-is [PSCustomObject]` er sant for tilnærmet alt i PowerShell. Tallet `5` ble
  til en tom hashtable og strengen `'0301'` til `@{ Length = 4 }`. Kroppen så
  riktig ut i PowerShell, men ble serialisert til `{"categoryIds":{}}` — og
  API-et filtrerte på ingenting. Vi ville målt ufiltrerte spørringer og trodd
  kombinasjonene var raske.
- En funksjon som returnerer `@(11)` pakker ut lista til skalaren `11`.
  `"taxonIds": 11` binder ikke til `int[]` i `System.Text.Json`.

## Forholdet til de eldre skriptene

`Scripts/PerfTestAreaCounts.ps1` og `Scripts/PerfTestListView.ps1` er beholdt som
de er. De er raske og målrettede, og nyttige når man jobber med ett konkret
problem. De har fortsatt sine egne kopier av oppdagelseskoden — en migrering til
`PerfLib.psm1` hører til sin egen endring.
