#Requires -Version 7.0
<#
    Generering av testmatrisen.

    Dot-sources av Run.ps1. Krever at PerfLib er importert og at Fixtures.ps1 er
    dot-sourcet foerst.

    HVORFOR GENERERE I STEDET FOR AA SKRIVE FOR HAAND
    Vi har 17 filterdimensjoner. Det gir 136 par og over 600 tripler. De gamle
    skriptene dekket kanskje aatte kombinasjoner, haandplukket. Da finner vi bare
    de trege kombinasjonene vi allerede mistenkte.

    Matrisen bygges i lag, og lagene kjoeres i rekkefoelge. Det gjoer en avbrutt
    kjoering brukbar: enkeltfiltrene er ferdig maalt lenge foer triplene starter.

      enkelt     - hvert nivaa av hver dimensjon alene
      par        - alle dimensjonspar, paa tunge og lette nivaaer
      trippel    - deterministisk utvalg av tre dimensjoner
      kvadruppel - deterministisk utvalg av fire
      reise      - haandplukkede, realistiske brukerflyter med mange filtre
      oppslag    - typeahead-endepunktene, som ikke tar filterkropp

    Lagene deler seg ogsaa etter hvilken kodesti de treffer. Omraadebufferen dekker
    ett og to filtre; trippel og kvadruppel gaar garantert til databasen. Enkelt- og
    par-laget er blandet, fordi kommune, fylke, havomraade, katalognr og takson under
    ordensniva blokkerer bufferen uansett hvor faa filtre som er satt.

    Utvalget av tripler er deterministisk (fast seed i Get-PerfShuffledSample).
    Uten det ville kjoering N ikke kunne sammenlignes med N-1, som er hele poenget.
#>

function Merge-PerfFilter {
    <#
        Slaar sammen filterfragmentene fra flere nivaaer til én request-kropp.
        Nivaaer fra samme dimensjon kombineres aldri, saa ingen noekler kolliderer.
    #>
    param([object[]] $Tiers)

    $body = @{}
    foreach ($tier in $Tiers) {
        $fragment = ConvertTo-PerfHashtable $tier.filter
        foreach ($key in $fragment.Keys) { $body[$key] = $fragment[$key] }
    }
    return $body
}

function New-PerfCase {
    param(
        [Parameter(Mandatory)] [string]   $Layer,
        [Parameter(Mandatory)] [object[]] $Tiers,
        [string] $KeyOverride
    )

    $tiers = @($Tiers | Where-Object { $null -ne $_ })
    if ($tiers.Count -eq 0) { return $null }

    # Noekkelen sorteres slik at den er den samme uansett hvilken rekkefoelge
    # dimensjonene ble plukket i. Den er avledet av dimensjon og nivaa, ikke av
    # navnet paa det oppslaatte elementet — ellers ville sammenligningen mot en
    # baseline brytes hver gang "tyngste institusjon" bytter identitet.
    $key = if ($KeyOverride) { $KeyOverride }
           else { (@($tiers | ForEach-Object { $_.key } | Sort-Object) -join '+') }

    return [PSCustomObject]@{
        CaseKey     = $key
        Layer       = $Layer
        Dimensions  = (@($tiers | ForEach-Object { ($_.key -split ':')[0] } | Sort-Object -Unique) -join ',')
        Label       = (@($tiers | ForEach-Object { $_.label }) -join ' | ')
        Filter      = Merge-PerfFilter -Tiers $tiers
        LookupPath  = $null
    }
}

function Get-PerfTierByRole {
    param([object] $Dimension, [string] $Role)
    return @($Dimension.tiers | Where-Object { $_.role -eq $Role }) | Select-Object -First 1
}

function Get-PerfTierByKey {
    param([object[]] $Dimensions, [string] $Key)
    foreach ($d in $Dimensions) {
        $hit = @($d.tiers | Where-Object { $_.key -eq $Key }) | Select-Object -First 1
        if ($hit) { return $hit }
    }
    return $null
}

function New-PerfCaseMatrix {
    <#
        Bygger hele case-lista for det valgte nivaaet.

        -Level styrer bredden:
          Quick    enkeltfiltre og oppslag. Minutter.
          Standard + alle par paa tunge nivaaer, et utvalg tripler, reiser.
          Full     + alle par ogsaa paa lette nivaaer, langt flere tripler.
    #>
    param(
        [Parameter(Mandatory)] [object] $Fixtures,
        [ValidateSet('Quick', 'Standard', 'Full')] [string] $Level = 'Standard',
        [int] $Seed = 20260909
    )

    $dims  = @($Fixtures.dimensions)
    $cases = [System.Collections.Generic.List[object]]::new()

    # --- Lag 1: enkeltfiltre ------------------------------------------------
    # Referansecasen uten filter maa vaere med. Endrer den seg, er det ikke
    # filtrene som har blitt tregere, men noe under dem.
    $cases.Add([PSCustomObject]@{
        CaseKey = 'ingen-filter'; Layer = 'enkelt'; Dimensions = ''
        Label = 'referanse uten filter'; Filter = @{}; LookupPath = $null
    })

    foreach ($d in $dims) {
        foreach ($tier in $d.tiers) {
            $case = New-PerfCase -Layer 'enkelt' -Tiers @($tier)
            if ($case) { $cases.Add($case) }
        }
    }

    # --- Lag 2: alle dimensjonspar -----------------------------------------
    # Begge roller er tatt med med vilje. Omraadetellingene er tregest for TUNGE
    # filtre, listevisningen for LETTE. Ett av nivaaene ville skjult halvparten
    # av problemene.
    if ($Level -ne 'Quick') {
        $roller = if ($Level -eq 'Full') { @('tung', 'lett') } else { @('tung') }

        for ($i = 0; $i -lt $dims.Count; $i++) {
            for ($j = $i + 1; $j -lt $dims.Count; $j++) {
                foreach ($rolle in $roller) {
                    $a = Get-PerfTierByRole -Dimension $dims[$i] -Role $rolle
                    $b = Get-PerfTierByRole -Dimension $dims[$j] -Role $rolle
                    if (-not $a -or -not $b) { continue }

                    $case = New-PerfCase -Layer 'par' -Tiers @($a, $b)
                    if ($case) { $cases.Add($case) }
                }
            }
        }
    }

    # --- Lag 3: tripler -----------------------------------------------------
    # Alle tripler ville vaert over 600 kombinasjoner ganget med endepunktene.
    # Vi trekker et deterministisk utvalg i stedet.
    #
    # Tre eller flere filtre er den ENE stien omraadebufferen aldri dekker: den har
    # ett- og toernivaa, ikke tre. Blokkerte filtre (kommune, fylke, havomraade,
    # katalognr, takson under ordensniva) gaar ogsaa til databasen, men de finnes
    # allerede i mengde blant enkelt- og par-casene. Rene "for mange filtre"-case
    # var derimot bare 51 av 519, saa utvalget er doblet.
    if ($Level -ne 'Quick') {
        $antallTripler = if ($Level -eq 'Full') { 300 } else { 60 }

        $alleTripler = [System.Collections.Generic.List[object]]::new()
        for ($i = 0; $i -lt $dims.Count; $i++) {
            for ($j = $i + 1; $j -lt $dims.Count; $j++) {
                for ($k = $j + 1; $k -lt $dims.Count; $k++) {
                    $alleTripler.Add(@($dims[$i], $dims[$j], $dims[$k]))
                }
            }
        }

        $valgte = Get-PerfShuffledSample -Items $alleTripler.ToArray() -Count $antallTripler -Seed $Seed

        $n = 0
        foreach ($trippel in $valgte) {
            # Veksler rolle slik at utvalget dekker begge endene av
            # selektivitetsskalaen, ikke bare den ene.
            $rolle = if ($n % 2 -eq 0) { 'tung' } else { 'lett' }
            $n++

            $tiers = @($trippel | ForEach-Object {
                (Get-PerfTierByRole -Dimension $_ -Role $rolle) ?? ($_.tiers[0])
            })
            if (@($tiers | Where-Object { $_ }).Count -lt 3) { continue }

            $case = New-PerfCase -Layer 'trippel' -Tiers $tiers
            if ($case) { $cases.Add($case) }
        }
    }

    # --- Lag 4: kvadrupler --------------------------------------------------
    # Fire dimensjoner. Samme begrunnelse som triplene, men lenger ut: her er
    # tellingen garantert databasedrevet, og filtrene skjaerer hverandre saa smalt
    # at optimalisereren maa velge rekkefoelge paa predikatene. Det er der
    # bitmap-selektiviteten historisk har bommet.
    #
    # Eget seed-tillegg slik at utvalget ikke blir de samme dimensjonene som
    # triplene bare med én til paa slutten.
    if ($Level -ne 'Quick') {
        $antallKvadrupler = if ($Level -eq 'Full') { 120 } else { 30 }

        $alleKvadrupler = [System.Collections.Generic.List[object]]::new()
        for ($i = 0; $i -lt $dims.Count; $i++) {
            for ($j = $i + 1; $j -lt $dims.Count; $j++) {
                for ($k = $j + 1; $k -lt $dims.Count; $k++) {
                    for ($l = $k + 1; $l -lt $dims.Count; $l++) {
                        $alleKvadrupler.Add(@($dims[$i], $dims[$j], $dims[$k], $dims[$l]))
                    }
                }
            }
        }

        $valgteKvad = Get-PerfShuffledSample -Items $alleKvadrupler.ToArray() `
            -Count $antallKvadrupler -Seed ($Seed + 1)

        $n = 0
        foreach ($kvad in $valgteKvad) {
            $rolle = if ($n % 2 -eq 0) { 'tung' } else { 'lett' }
            $n++

            $tiers = @($kvad | ForEach-Object {
                (Get-PerfTierByRole -Dimension $_ -Role $rolle) ?? ($_.tiers[0])
            })
            if (@($tiers | Where-Object { $_ }).Count -lt 4) { continue }

            $case = New-PerfCase -Layer 'kvadruppel' -Tiers $tiers
            if ($case) { $cases.Add($case) }
        }
    }

    # --- Lag 5: realistiske brukerflyter ------------------------------------
    # Haandplukket, i motsetning til resten. Disse skal ligne det brukerne
    # faktisk gjoer, og er de casene det er verdt aa se paa foerst i rapporten.
    if ($Level -ne 'Quick') {
        $reiser = @(
            @{ Navn = 'reise:artsjakt';       Keys = @('takson:art-lett', 'fylke:tung', 'periode:lett') },
            @{ Navn = 'reise:forvaltning';    Keys = @('kategori:tung', 'kommune:tung', 'periode:tung') },
            @{ Navn = 'reise:institusjon';    Keys = @('institusjon:lett', 'periode:tung', 'bilder:tung') },
            @{ Navn = 'reise:verneomraade';   Keys = @('verneomraade:tung', 'taksongruppe:tung', 'periode:lett') },
            @{ Navn = 'reise:marin';          Keys = @('havomraade:tung', 'taksongruppe:tung', 'funntype:tung') },
            @{ Navn = 'reise:kartlegging';    Keys = @('takson:Order', 'kommune:mange', 'koordpresisjon:lett', 'periode:tung') },
            @{ Navn = 'reise:datasett';       Keys = @('datasett:tung', 'fylke:tung', 'periode:tung') },
            @{ Navn = 'reise:alt-paa-en-gang'; Keys = @(
                'takson:Order', 'kategori:tung', 'taksongruppe:tung', 'institusjon:tung',
                'kommune:mange', 'periode:tung', 'koordpresisjon:tung', 'funntype:tung') }
        )

        foreach ($reise in $reiser) {
            $tiers = @($reise.Keys | ForEach-Object { Get-PerfTierByKey -Dimensions $dims -Key $_ } | Where-Object { $_ })

            # Reisen droppes hvis noe mangler. Et delvis filter ville faatt samme
            # navn som den fulle reisen i baselinen, og sammenligningen ville
            # sammenlignet to ulike spoerringer.
            if ($tiers.Count -ne $reise.Keys.Count) {
                Write-Warning "$($reise.Navn): mangler nivaa, hoppes over"
                continue
            }

            $case = New-PerfCase -Layer 'reise' -Tiers $tiers -KeyOverride $reise.Navn
            if ($case) { $cases.Add($case) }
        }
    }

    # --- Lag 5: oppslag (typeahead) ----------------------------------------
    # Strengsoeket forsvant ikke fra loesningen, det flyttet hit da filtrene gikk
    # over til aa ta IDer. Maales derfor for seg: gevinsten i filterspoerringen er
    # ikke reell hvis kostnaden bare er flyttet til oppslaget.
    #
    # Blir katalognummer tregt, sjekk om soeket har blitt gjort om til delstreng
    # ('%x%') — det kan ikke bruke IX_Observation_CatalogNumber.
    $oppslag = @(
        @{ Key = 'oppslag:datasett';        Path = '/api/Lookup/Datasets?search=a'          },
        @{ Key = 'oppslag:prosjekt';        Path = '/api/Lookup/Projects?search=a'          },
        @{ Key = 'oppslag:institusjoner';   Path = '/api/Lookup/Institutions'               },
        @{ Key = 'oppslag:katalognr-kort';  Path = '/api/Lookup/CatalogNumbers?search=12'   },
        @{ Key = 'oppslag:katalognr-lang';  Path = '/api/Lookup/CatalogNumbers?search=123456' },
        @{ Key = 'oppslag:artstre-rot';     Path = '/api/Lookup/TaxonTree'                  },
        @{ Key = 'oppslag:omraader';        Path = '/api/Lookup/Areas'                      }
    )

    foreach ($o in $oppslag) {
        $cases.Add([PSCustomObject]@{
            CaseKey = $o.Key; Layer = 'oppslag'; Dimensions = ''
            Label = $o.Path; Filter = $null; LookupPath = $o.Path
        })
    }

    return $cases
}

function Test-PerfEndpointRunsCase {
    <#
        Skal dette endepunktet kjoeres for dette caset?

        Uten begrensninger kjoeres hvert endepunkt for hvert case. To valgfrie
        egenskaper snevrer inn:

          Layers   - bare case i disse lagene (enkelt, par, trippel, ...)
          CaseKeys - bare disse konkrete casene

        Er begge satt maa begge stemme. Oppslagscasene gaar aldri hit; de kjoerer
        sitt eget GET-kall i Run.ps1.
    #>
    param([Parameter(Mandatory)] $Endpoint, [Parameter(Mandatory)] $Case)

    if ($Endpoint.Layers   -and $Endpoint.Layers   -notcontains $Case.Layer)   { return $false }
    if ($Endpoint.CaseKeys -and $Endpoint.CaseKeys -notcontains $Case.CaseKey) { return $false }
    return $true
}

function New-PerfEndpointMatrix {
    <#
        Hvilke endepunkter hvert filter kjoeres mot.

        AREAMARKERS KJOERES BARE UFILTRERT
        Frontend henter AreaMarkers noeyaktig to ganger per sidelast, ufiltrert,
        ett kall per zoomnivaa (prefetchAreaGeometries). Geometrien legges saa i en
        klientside cache som aldri toemmes, og alle senere filterendringer gaar til
        AreaCounts. Filtrert AreaMarkers forekommer ikke.

        Suiten maalte tidligere 1012 filtrerte AreaMarkers-kall. De maalte riktignok
        tellestien, men med geometrilasting, WKT-konvertering og 21 MB serialisering
        oppaa - stoey som skjulte det vi faktisk ville maale. CaseKeys = ingen-filter
        kjoerer dem bare for referansecaset.

        AREACOUNTS ER METRIKKEN FOR TELLESTIEN
        Samme FilterAndComputeCounts som AreaMarkers, uten geometri. Endepunktet har
        fem minutters minnecache per filter (SearchService), saa bare FOERSTE kall
        per filter maaler databasen; kjoering 2 og utover maaler cachen.

        Derfor Runs = 1. Hvert case har sitt eget filter, saa foerste kall er alltid
        kaldt. Measure-PerfRequest faller tilbake til den kalde tiden naar det ikke
        finnes varme kjoeringer, saa BAADE ColdMs og WarmMs blir det kalde tallet -
        og Compare.ps1, som sammenligner paa WarmMs, trenger ingen endring.

        LOKASJONSUTSNITTENE ER IKKE LIKEVERDIGE
        16 utsnitt x 2 endepunkter x 777 case ville gitt 24 864 maalinger - mer enn
        fire ganger hele dagens kjoering, for to endepunkter. Utsnittene har derfor
        ulik rolle, styrt av Layers/CaseKeys:

          oslo         hele filtermatrisen. Tyngste utsnitt (967 649 lokasjoner),
                       og det eneste stedet vi vil se hvordan filtre kombinerer seg.
          trondheim    enkelt + par. Fanger planvipping mellom to utsnitt av samme
                       stoerrelse uten aa doble hele matrisen.
          kristiansand enkelt. Eneste store utsnitt under MaxResults — her maa
                       TOP-en rangere alle gruppene, ikke bare kutte.
          trollheimen  enkelt. Spredt fjellomraade, motsatt tetthetsprofil av byene.
          stigen       et fast utvalg paa ti case. De tolv utsnittene skal svare paa
                       hvordan endepunktet skalerer med antall lokasjoner, og da er
                       777 filtervarianter per utsnitt stoey, ikke signal.

        LOCATIONPOLYGONS FOELGER LOCATIONS
        Samme filterkropp, samme utsnitt, samme kodesti fram til geometrien hentes.
        Kjoerer den ikke paa de samme utsnittene, kan vi ikke se om en regresjon
        ligger i lokasjonssoeket eller i polygonhentingen.
    #>
    param(
        [ValidateSet('Quick', 'Standard', 'Full')] [string] $Level = 'Standard',
        [object] $Envelopes,
        [int]    $ResultsPerPage = 10
    )

    $env = ConvertTo-PerfHashtable $Envelopes
    $m   = [System.Collections.Generic.List[object]]::new()

    # Listevisningen. Side 1 er det brukeren moeter; en dyp side er der OFFSET
    # begynner aa koste, fordi sorteringen paa Observation.Id maa telle seg forbi
    # radene en for en.
    $m.Add(@{ Endpoint = 'Observation'; Variant = 'side1'
              Path = '/api/Search/Observation'
              Extra = @{ pageNumber = 1; resultsPerPage = $ResultsPerPage } })

    # Kartmarkoerene paa laveste zoom - faerrest celler, mest aggregering per celle.
    $m.Add(@{ Endpoint = 'AreaMarkers'; Variant = 'z1'; Path = '/api/Search/AreaMarkers?zoomLevel=1'; Extra = @{}; CaseKeys = @('ingen-filter') })
    $m.Add(@{ Endpoint = 'AreaCounts';  Variant = 'z1'; Path = '/api/Search/AreaCounts?zoomLevel=1';  Extra = @{}; Runs = 1 })

    if ($Level -ne 'Quick') {
        $m.Add(@{ Endpoint = 'Observation'; Variant = 'side50'
                  Path = '/api/Search/Observation'
                  Extra = @{ pageNumber = 50; resultsPerPage = $ResultsPerPage } })
    }

    # LOKASJONSUTSNITT PER NIVAA
    # Nivaaene er en trapp, ikke av/paa. Arbeidsflyten er: Quick for en kjapp
    # bekreftelse paa at en ytelsesendring virker, Standard hvis det ser bra ut,
    # Full naar noe skal dokumenteres.
    #
    # Derfor maa Quick ha ETT utsnitt fra hvert tetthetsnivaa - en forbedring som
    # bare slaar inn paa store svar, eller bare paa smaa, skal synes med én gang.
    # Tidligere kjoerte Quick ingen lokasjoner i det hele tatt, og Standard hoppet
    # rett til de fire tyngste.
    #
    # Hvert utsnitt sier hva det kjoerer per nivaa:
    #   $null   - ikke med paa dette nivaaet
    #   @{}     - alle case
    #   @{ Layers = ... } / @{ CaseKeys = ... } - snevret inn, se
    #   Test-PerfEndpointRunsCase

    # Quick: seks case som spenner spekteret. Referansen, det verste
    # omraadefilteret (fylke gir en OR mellom omraadetype 2 og 6 som columnstore
    # ikke kan pushe ned), to brede listefiltre, taksonhierarkiet, og ett
    # selektivt som fanger regresjoner i den raske stien.
    $quickCases = @(
        'ingen-filter', 'fylke:tung', 'periode:tung',
        'taksongruppe:tung', 'takson:Order', 'taksongruppe:lett'
    )

    # Stigens faste utvalg: referansen, de fem enkeltfiltrene som har vist seg
    # tregest i maalingene (fylke, periode, regstatus, funntype, kategori), to
    # brede taksonfiltre og to reiser. Nok til aa se om tetthet og filter spiller
    # sammen, uten aa gjenta hele matrisen seksten ganger.
    $stigeCases = @(
        'ingen-filter', 'fylke:tung', 'periode:tung', 'regstatus:tung',
        'funntype:tung', 'kategori:tung', 'taksongruppe:tung', 'takson:Order',
        'reise:forvaltning', 'reise:kartlegging'
    )

    $qQuick = @{ CaseKeys = $quickCases }
    $qStige = @{ CaseKeys = $stigeCases }

    $lokasjonsUtsnitt = @(
        # De fire store. Paa Quick er oslo (over taket) og kristiansand (under)
        # med; de to andre foerst fra Standard.
        @{ Navn='oslo';         Quick=$qQuick; Standard=@{ Layers=@('enkelt') }; Full=@{} }
        @{ Navn='trondheim';    Quick=$null;   Standard=$qStige; Full=@{ Layers=@('enkelt','par') } }
        @{ Navn='trollheimen';  Quick=$null;   Standard=$qStige; Full=@{ Layers=@('enkelt') } }
        @{ Navn='kristiansand'; Quick=$qQuick; Standard=$qStige; Full=@{ Layers=@('enkelt') } }

        # Tetthetsstigen. Ett fra hvert nivaa er med paa Quick, alle fra Standard.
        @{ Navn='telemark50k';  Quick=$qQuick; Standard=$qStige; Full=$qStige }
        @{ Navn='innlandet50k'; Quick=$null;   Standard=$qStige; Full=$qStige }
        @{ Navn='ostfold50k';   Quick=$null;   Standard=$qStige; Full=$qStige }

        @{ Navn='salten10k';    Quick=$qQuick; Standard=$qStige; Full=$qStige }
        @{ Navn='ostfold10k';   Quick=$null;   Standard=$qStige; Full=$qStige }
        @{ Navn='valdres10k';   Quick=$null;   Standard=$qStige; Full=$qStige }

        @{ Navn='gudbrand1k';   Quick=$qQuick; Standard=$qStige; Full=$qStige }
        @{ Navn='varanger1k';   Quick=$null;   Standard=$qStige; Full=$qStige }
        @{ Navn='agder1k';      Quick=$null;   Standard=$qStige; Full=$qStige }

        @{ Navn='mjosa100';     Quick=$qQuick; Standard=$qStige; Full=$qStige }
        @{ Navn='namdal100';    Quick=$null;   Standard=$qStige; Full=$qStige }
        @{ Navn='telemark100';  Quick=$null;   Standard=$qStige; Full=$qStige }
    )

    foreach ($u in $lokasjonsUtsnitt) {
        $spec = $u[$Level]
        if ($null -eq $spec) { continue }

        $e = $env[$u.Navn]
        if (-not $e) {
            Write-Warning "Utsnittet $($u.Navn) finnes ikke i fixtures, hoppes over"
            continue
        }

        foreach ($endepunkt in @('Locations', 'LocationPolygons')) {
            $m.Add(@{ Endpoint = $endepunkt; Variant = $u.Navn
                      Path = "/api/Search/$endepunkt"
                      Extra = @{ envelope = $e }
                      Layers = $spec.Layers; CaseKeys = $spec.CaseKeys })
        }
    }

    if ($Level -eq 'Full') {
        $m.Add(@{ Endpoint = 'AreaMarkers'; Variant = 'z2'; Path = '/api/Search/AreaMarkers?zoomLevel=2'; Extra = @{}; CaseKeys = @('ingen-filter') })
        $m.Add(@{ Endpoint = 'AreaCounts';  Variant = 'z2'; Path = '/api/Search/AreaCounts?zoomLevel=2';  Extra = @{}; Runs = 1 })

        $m.Add(@{ Endpoint = 'Observation'; Variant = 'side1-stor'
                  Path = '/api/Search/Observation'
                  Extra = @{ pageNumber = 1; resultsPerPage = 100 } })
    }

    return $m
}
