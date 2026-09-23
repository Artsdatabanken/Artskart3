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
        oppaa - stoey som skjulte det vi faktisk ville maale. Merket OnlyUnfiltered
        kjoerer dem bare for ingen-filter-caset.

        AREACOUNTS ER METRIKKEN FOR TELLESTIEN
        Samme FilterAndComputeCounts som AreaMarkers, uten geometri. Endepunktet har
        fem minutters minnecache per filter (SearchService), saa bare FOERSTE kall
        per filter maaler databasen; kjoering 2 og utover maaler cachen.

        Derfor Runs = 1. Hvert case har sitt eget filter, saa foerste kall er alltid
        kaldt. Measure-PerfRequest faller tilbake til den kalde tiden naar det ikke
        finnes varme kjoeringer, saa BAADE ColdMs og WarmMs blir det kalde tallet -
        og Compare.ps1, som sammenligner paa WarmMs, trenger ingen endring.
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
    # radene én for én.
    $m.Add(@{ Endpoint = 'Observation'; Variant = 'side1'
              Path = '/api/Search/Observation'
              Extra = @{ pageNumber = 1; resultsPerPage = $ResultsPerPage } })

    # Kartmarkoerene paa laveste zoom — faerrest celler, mest aggregering per celle.
    $m.Add(@{ Endpoint = 'AreaMarkers'; Variant = 'z1'; Path = '/api/Search/AreaMarkers?zoomLevel=1'; Extra = @{}; OnlyUnfiltered = $true })
    $m.Add(@{ Endpoint = 'AreaCounts';  Variant = 'z1'; Path = '/api/Search/AreaCounts?zoomLevel=1';  Extra = @{}; Runs = 1 })

    if ($Level -ne 'Quick') {
        $m.Add(@{ Endpoint = 'Observation'; Variant = 'side50'
                  Path = '/api/Search/Observation'
                  Extra = @{ pageNumber = 50; resultsPerPage = $ResultsPerPage } })

        $m.Add(@{ Endpoint = 'Locations'; Variant = 'norge'
                  Path = '/api/Search/Locations'
                  Extra = @{ envelope = $env.norge } })
    }

    if ($Level -eq 'Full') {
        $m.Add(@{ Endpoint = 'AreaMarkers'; Variant = 'z2'; Path = '/api/Search/AreaMarkers?zoomLevel=2'; Extra = @{}; OnlyUnfiltered = $true })
        $m.Add(@{ Endpoint = 'AreaCounts';  Variant = 'z2'; Path = '/api/Search/AreaCounts?zoomLevel=2';  Extra = @{}; Runs = 1 })

        $m.Add(@{ Endpoint = 'Locations'; Variant = 'oslo'
                  Path = '/api/Search/Locations'
                  Extra = @{ envelope = $env.oslo } })

        $m.Add(@{ Endpoint = 'Observation'; Variant = 'side1-stor'
                  Path = '/api/Search/Observation'
                  Extra = @{ pageNumber = 1; resultsPerPage = 100 } })
    }

    return $m
}
