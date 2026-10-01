#Requires -Version 7.0
<#
    Oppdagelse og lagring av testdata for ytelsessuiten.

    Dot-sources av Run.ps1. Krever at PerfLib er importert foerst.

    HVORFOR FRYSE TESTDATAENE
    Suiten skal kunne svare paa "ble noe tregere siden sist". Da maa casene vaere
    de samme mellom kjoeringer. Hvis vi slaar opp "tyngste institusjon" paa nytt
    hver gang, kan den bytte identitet naar data vokser, og sammenligningen maaler
    noe helt annet enn den tror.

    Derfor skrives det oppdagede utvalget til fixtures.json og gjenbrukes.
    -RefreshFixtures regenererer bevisst, og det boer gjoeres sjelden — helst
    samtidig som man forkaster gamle baselinjer.

    EN DIMENSJON er ett filterfelt (kommune, takson, periode ...).
    ET NIVAA (tier) er én konkret verdi for den dimensjonen, med en rolle:
      tung  - lav selektivitet, mange treff
      lett  - hoey selektivitet, faa treff
      annen - varianter vi vil dekke, men ikke bruke i parkombinasjonene

    Rollene styrer parkombinasjonene. Begge trengs: omraadetellingene er tregest
    for TUNGE filtre (mye aa aggregere), mens listevisningen er tregest for LETTE
    (TOP-en maa lese langt ned i klyngeindeksen foer den er fylt).
#>

# Kartutsnitt i EPSG:25833 (UTM 33N). Brukes av Locations og LocationPolygons.
#
# HVORFOR AKKURAT DISSE
# Frontend henter lokasjoner foerst fra OpenLayers-zoom 11 (ZoomConfig, ApiZoomLevel
# .LocationPoints). Det stoerste utsnittet endepunktet noen gang faar er dermed
# 185 x 103 km — maalt i kartet, ikke antatt. Alt over det er en kallform som ikke
# finnes.
#
# De gamle utsnittene var norge (1200 x 1510 km) og trondelag (200 x 200 km), begge
# over taket, pluss oslo paa 40 km. Verre: omraadefixturene laa ingen steder i
# naerheten av oslo-utsnittet — Farsund, Trondelag, Nordland og Troms ga alle NULL
# lokasjoner der. 419 av 777 oslo-case maalte altsaa et tomt svar, og fylke:tung
# brukte 2005 ms paa aa returnere ingenting.
#
# TO GRUPPER
# store  - fire utsnitt der endepunktet har mest aa gjoere. oslo, trondheim og
#          trollheimen ligger over MaxResults og returnerer 100 000 rader uansett
#          hvor mange lokasjoner utsnittet inneholder. kristiansand ligger like
#          under, og er det eneste store utsnittet der TOP-en faktisk maa rangere
#          alle gruppene i stedet for bare aa kutte.
#          trondheim og trollheimen er hentet rett fra kartet; oslo har samme
#          stoerrelse, sentrert paa tyngdepunktet til lokasjonene i kommunen.
# stige  - tolv mindre utsnitt valgt slik at de gir ca. 50 000, 10 000, 1 000 og
#          100 lokasjoner, tre av hver. De maaler hvordan endepunktet skalerer med
#          tetthet, ikke med filter.
#
# Alle utsnitt er disjunkte, saa ingen maaling kan lese data en tidligere maaling
# nettopp varmet opp. Eneste unntak er trollheimen/trondheim, som overlapper fordi
# begge er hentet direkte fra kartet.
#
# Lokasjonstallene er hvor mange lokasjoner som FINNES i utsnittet, ikke hvor mange
# endepunktet returnerer. MaxResults er 100 000 (LocationSearchFilterDto), saa alt
# over det kuttes. Tallene er maalt mot Artskart3IndexProdLikeTestMigrations og
# staar som dokumentasjon - de brukes ikke av koden.
$script:PerfEnvelopes = [ordered]@{
    # store: tre paa zoom 11-takstoerrelsen (185 x 103 km), alle over MaxResults
    oslo         = @{ minX =  170566; maxX =  355820; minY = 6599473; maxY = 6702091 }  # 967 649 lok, kuttes
    trondheim    = @{ minX =  191505; maxX =  376758; minY = 6987009; maxY = 7089626 }  # 274 481, kuttes
    trollheimen  = @{ minX =  148449; maxX =  297444; minY = 6946372; maxY = 7028906 }  # 158 550, kuttes
    # ... og ett like under taket, saa vi ogsaa maaler et stort svar som IKKE kuttes
    kristiansand = @{ minX =   40000; maxX =  120000; minY = 6400000; maxY = 6480000 }  #  90 278

    # stige: ca. 50 000 lokasjoner
    telemark50k  = @{ minX =  200000; maxX =  240000; minY = 6520000; maxY = 6560000 }  #  49 939
    innlandet50k = @{ minX =  240000; maxX =  280000; minY = 6760000; maxY = 6800000 }  #  46 557
    ostfold50k   = @{ minX =  300000; maxX =  320000; minY = 6560000; maxY = 6580000 }  #  45 076

    # stige: ca. 10 000
    salten10k    = @{ minX =  400000; maxX =  440000; minY = 7520000; maxY = 7560000 }  #  10 046
    ostfold10k   = @{ minX =  270000; maxX =  275000; minY = 6570000; maxY = 6575000 }  #  10 024
    valdres10k   = @{ minX =  120000; maxX =  160000; minY = 6760000; maxY = 6800000 }  #   9 955

    # stige: ca. 1 000
    gudbrand1k   = @{ minX =  200000; maxX =  210000; minY = 6800000; maxY = 6810000 }  #   1 001
    varanger1k   = @{ minX = 1070000; maxX = 1075000; minY = 7855000; maxY = 7860000 }  #   1 000
    agder1k      = @{ minX =   50000; maxX =   60000; minY = 6570000; maxY = 6580000 }  #   1 000

    # stige: ca. 100
    mjosa100     = @{ minX =  208000; maxX =  210000; minY = 6838000; maxY = 6840000 }  #     101
    namdal100    = @{ minX =  330000; maxX =  331000; minY = 7112000; maxY = 7113000 }  #     100
    telemark100  = @{ minX =  126000; maxX =  127000; minY = 6593000; maxY = 6594000 }  #     100
}

function ConvertTo-PerfHashtable {
    <#
        JSON-runden gjoer hashtables om til PSCustomObject. Filterfragmentene maa
        tilbake til hashtable for aa kunne slaas sammen til en request-kropp.
    #>
    param([object] $InputObject)

    if ($null -eq $InputObject) { return $null }

    # MERK: rekkefoelgen paa sjekkene er ikke tilfeldig.
    #
    # '-is [PSCustomObject]' er sant for tilnaermet ALT i PowerShell, fordi
    # PSCustomObject er PSObject og alle objekter kan ses som PSObject. Testes
    # den foerst, blir tallet 5 til en tom hashtable og strengen '0301' til
    # @{ Length = 4 } — filterkroppen ser riktig ut i PowerShell, men
    # serialiseres til {} og [{"Length":4}] i JSON, og API-et filtrerer paa
    # ingenting.
    #
    # Enkle verdier og samlinger maa derfor tas foerst, og et ekte
    # PSCustomObject kjennes igjen paa typenavnet.
    if ($InputObject -is [string] -or $InputObject -is [ValueType]) { return $InputObject }

    if ($InputObject -is [System.Collections.IDictionary]) {
        $result = @{}
        foreach ($key in $InputObject.Keys) { $result[$key] = ConvertTo-PerfHashtable $InputObject[$key] }
        return $result
    }

    if ($InputObject -is [System.Collections.IEnumerable]) {
        # Komma foran: uten den pakker PowerShell ut en ett-elements liste til en
        # skalar paa vei ut av funksjonen. @(11) ville blitt 11, og filteret
        # serialisert som "taxonIds": 11 i stedet for "taxonIds": [11] — som
        # System.Text.Json ikke binder til int[].
        return , @(@($InputObject) | ForEach-Object { ConvertTo-PerfHashtable $_ })
    }

    if ($InputObject.PSObject.TypeNames[0] -eq 'System.Management.Automation.PSCustomObject') {
        $result = @{}
        foreach ($prop in $InputObject.PSObject.Properties) {
            $result[$prop.Name] = ConvertTo-PerfHashtable $prop.Value
        }
        return $result
    }

    return $InputObject
}

function New-PerfTier {
    param(
        [Parameter(Mandatory)] [string]    $Key,
        [Parameter(Mandatory)] [string]    $Role,
        [Parameter(Mandatory)] [hashtable] $Filter,
        [string] $Label = ''
    )

    return [PSCustomObject]@{
        key    = $Key
        role   = $Role
        label  = $Label
        filter = $Filter
    }
}

function New-PerfDimension {
    param(
        [Parameter(Mandatory)] [string]   $Name,
        [Parameter(Mandatory)] [object[]] $Tiers
    )

    $tiers = @($Tiers | Where-Object { $null -ne $_ })
    if ($tiers.Count -eq 0) { return $null }

    return [PSCustomObject]@{ name = $Name; tiers = $tiers }
}

function Get-PerfSpreadTiers {
    <#
        Lager nivaaer for en dimensjon som har observationCount.

        ENKELTVERDIER: tung / median / lett, som foer.

        FLERE VERDIER I SAMME DIMENSJON: legges til naar $MultiFilterFactory er
        oppgitt. Ti av dimensjonene tar en array, men foer 11.09.2026 hadde bare
        fire av dem flerverdi-nivaaer - og alltid som «de N tyngste».
        Kombinasjonene tung+lett og median+lett var ikke dekket i det hele tatt.
        Det var hullet som gjorde at to institusjoner sammen kunne ta over et
        minutt i produksjon uten at suiten sa fra.
        (SQL blir IN (a, b), ikke to separate predikater - planen kan vippe fra
        seek til scan naar listen vokser, og spennet i selektivitet avgjoer.)

        Nye nivaaer faar rollen 'annen' med vilje: da havner de i enkelt-laget,
        men ikke i par- og trippellagene, som plukker paa 'tung' og 'lett'.
        Matrisen vokser dermed lineaert, ikke kvadratisk. Eksisterende
        case-noekler er uendret, saa sammenligning mot gamle baselinjer bestaar -
        de nye dukker bare opp som «nye case».

        $FilterFactory faar én id. $MultiFilterFactory faar en array av id-er.
    #>
    param(
        [Parameter(Mandatory)] [string]      $Name,
        [object]                             $Spread,
        [Parameter(Mandatory)] [scriptblock] $FilterFactory,
        [scriptblock]                        $MultiFilterFactory
    )

    if (-not $Spread) { return $null }

    $tiers = [System.Collections.Generic.List[object]]::new()
    $seen  = [System.Collections.Generic.HashSet[string]]::new()

    foreach ($item in @(
        @{ S = $Spread.Heaviest; Role = 'tung'   },
        @{ S = $Spread.Median;   Role = 'annen'  },
        @{ S = $Spread.Lightest; Role = 'lett'   })) {

        $sample = $item.S
        if (-not $sample) { continue }

        # Med faa elementer kan tung, median og lett vaere samme rad. Da ville vi
        # maalt samme spoerring tre ganger under tre navn.
        if (-not $seen.Add("$($sample.Id)")) { continue }

        $tiers.Add((New-PerfTier `
            -Key    "$Name`:$($item.Role)" `
            -Role   $item.Role `
            -Label  ("{0} ({1:N0} obs)" -f $sample.Name, $sample.Count) `
            -Filter (& $FilterFactory $sample.Id)))
    }

    # --- Flere verdier i samme dimensjon -----------------------------------
    if ($MultiFilterFactory) {
        # AllSamples, ikke All: den normaliserte lista har Id uavhengig av om
        # oppslaget kalte feltet 'id' eller 'fid'. Leses All direkte, faar
        # omraadene Area.Id i stedet for Fid, og filteret peker paa ingenting.
        $alle = @($Spread.AllSamples)

        # Posisjonene i den synkende lista. Median tas fra midten, ikke fra
        # $Spread.Median, slik at alle kombinasjonene leser samme kilde.
        $tung   = $alle[0]
        $lett   = $alle[-1]
        $median = $alle[[int]($alle.Count / 2)]

        $kombinasjoner = @(
            @{ Suffiks = 'x2-tung-tung';   Elementer = @($alle | Select-Object -First 2) },
            @{ Suffiks = 'x2-tung-lett';   Elementer = @($tung, $lett) },
            @{ Suffiks = 'x2-median-lett'; Elementer = @($median, $lett) },
            @{ Suffiks = 'x5-blandet';     Elementer = @(
                   $alle[0], $alle[[int]($alle.Count / 4)], $median,
                   $alle[[int]($alle.Count * 3 / 4)], $lett) }
        )

        foreach ($komb in $kombinasjoner) {
            $elementer = @($komb.Elementer | Where-Object { $null -ne $_ })

            # Unike id-er. Med faa elementer i dimensjonen kan tung, median og
            # lett falle sammen, og da ville «x2» vaert samme verdi to ganger -
            # altsaa et enkeltverdifilter under et navn som lover noe annet.
            $ider = @($elementer | ForEach-Object { $_.Id } | Select-Object -Unique)
            if ($ider.Count -lt 2) { continue }

            $sett = [System.Collections.Generic.HashSet[string]]::new()
            $etikett = (@($elementer | Where-Object { $sett.Add("$($_.Id)") } |
                          ForEach-Object { "{0} ({1:N0})" -f $_.Name, $_.Count }) -join ' + ')

            $tiers.Add((New-PerfTier `
                -Key    "$Name`:$($komb.Suffiks)" `
                -Role   'annen' `
                -Label  $etikett `
                -Filter (& $MultiFilterFactory $ider)))
        }
    }

    return New-PerfDimension -Name $Name -Tiers $tiers
}

function New-PerfFixtures {
    <#
        Kjoerer hele oppdagelsesfasen mot Lookup-endepunktene og bygger
        dimensjonskatalogen. Kalles kun ved foerste kjoering eller -RefreshFixtures.
    #>
    param([int] $TaxonSearchBudget = 80)

    Write-Host 'Bygger testdata (fixtures) fra Lookup-endepunktene...' -ForegroundColor Cyan

    $dimensions = [System.Collections.Generic.List[object]]::new()

    # --- Takson: ett nivaa per rang, pluss den letteste arten ---------------
    $taxa = Get-PerfTaxonSamples -MaxRequests $TaxonSearchBudget
    $taxonTiers = [System.Collections.Generic.List[object]]::new()
    $rankNames  = Get-PerfRankNames

    foreach ($rankId in (Get-PerfRankOrder)) {
        if (-not $taxa.Heavy.ContainsKey($rankId)) { continue }
        $s = $taxa.Heavy[$rankId]

        # Rollen 'tung' gis til Orden. Den er det tyngste nivaaet som fortsatt er
        # et realistisk brukervalg — Kingdom filtrerer i praksis ingenting bort.
        $role = if ($rankId -eq 11) { 'tung' } else { 'annen' }

        $taxonTiers.Add((New-PerfTier `
            -Key    "takson:$($rankNames[$rankId])" `
            -Role   $role `
            -Label  ("{0} ({1:N0} obs)" -f $s.Name, $s.Count) `
            -Filter @{ taxonIds = @($s.Id) }))
    }

    if ($taxa.Light.ContainsKey(22)) {
        $s = $taxa.Light[22]
        $taxonTiers.Add((New-PerfTier `
            -Key    'takson:art-lett' `
            -Role   'lett' `
            -Label  ("{0} ({1:N0} obs)" -f $s.Name, $s.Count) `
            -Filter @{ taxonIds = @($s.Id) }))
    }

    # Flere taksoner blir en UNION av delspoerringer — én per takson
    if ($taxa.Heavy.ContainsKey(19) -and $taxa.Heavy.ContainsKey(22)) {
        $taxonTiers.Add((New-PerfTier `
            -Key    'takson:union-x2' `
            -Role   'annen' `
            -Label  'slekt + art' `
            -Filter @{ taxonIds = @($taxa.Heavy[19].Id, $taxa.Heavy[22].Id) }))
    }

    $d = New-PerfDimension -Name 'takson' -Tiers $taxonTiers
    if ($d) { $dimensions.Add($d) }

    # --- Dimensjoner med observationCount fra Lookup ------------------------
    $lookups = @(
        @{ Name = 'taksongruppe'; Path = '/api/Lookup/TaxonGroups';    Nested = $null;         IdProp = 'id';  Field = 'taxonGroupIds'    },
        @{ Name = 'kategori';     Path = '/api/Lookup/Categories';     Nested = 'categories';  IdProp = 'id';  Field = 'categoryIds'      },
        @{ Name = 'institusjon';  Path = '/api/Lookup/Institutions';   Nested = $null;         IdProp = 'id';  Field = 'organizationIds'  },
        @{ Name = 'funntype';     Path = '/api/Lookup/BasisOfRecords'; Nested = $null;         IdProp = 'id';  Field = 'basisOfRecordIds' },
        @{ Name = 'atferd';       Path = '/api/Lookup/Behaviors';      Nested = $null;         IdProp = 'id';  Field = 'behaviorIds'      }
    )

    foreach ($lookup in $lookups) {
        try { $response = Invoke-PerfApi -Path $lookup.Path }
        catch { Write-Warning "Kunne ikke hente $($lookup.Path): $($_.Exception.Message)"; continue }

        $items  = if ($lookup.Nested) { @($response) | ForEach-Object { $_.$($lookup.Nested) } } else { $response }
        $spread = Get-PerfSelectivitySpread -Items $items -IdProperty $lookup.IdProp -Label $lookup.Name

        $field = $lookup.Field
        $d = Get-PerfSpreadTiers -Name $lookup.Name -Spread $spread `
            -FilterFactory {
                param($id) @{ $field = @([int]$id) }
            }.GetNewClosure() `
            -MultiFilterFactory {
                # Komma foran: uten den pakker PowerShell ut en ett-elements
                # liste til en skalar, og filteret blir "ids": 5 i stedet for [5].
                param($ider) @{ $field = @($ider | ForEach-Object { [int]$_ }) }
            }.GetNewClosure()

        if ($d) { $dimensions.Add($d); Write-Host ("  {0,-16} {1} nivaa" -f $lookup.Name, $d.tiers.Count) }
    }

    # --- Omraader -----------------------------------------------------------
    # AreaResponseDto pakker hver type i en AreaTypeDto med en areas-liste.
    # Verneomraader og havomraader var ikke dekket av de gamle skriptene i det
    # hele tatt. Verneomraader hoerer til de dyre: HasObservationAttributeFilters
    # inkluderer dem, mens fylker og kommuner bruker forhaandsberegnede antall.
    $areaTypes = @(
        @{ Name = 'fylke';        Prop = 'counties';        Field = 'countyIds'         },
        @{ Name = 'kommune';      Prop = 'municipalities';  Field = 'municipalityIds'   },
        @{ Name = 'verneomraade'; Prop = 'restrictedAreas'; Field = 'restrictedAreaIds' },
        @{ Name = 'havomraade';   Prop = 'oceanAreas';      Field = 'oceanAreaIds'      }
    )

    try { $areas = Invoke-PerfApi -Path '/api/Lookup/Areas' }
    catch { Write-Warning "Kunne ikke hente omraader: $($_.Exception.Message)"; $areas = $null }

    foreach ($areaType in $areaTypes) {
        if (-not $areas) { break }

        $items = $areas.$($areaType.Prop).areas
        if (-not $items) { Write-Warning "Ingen omraader av typen $($areaType.Name)"; continue }

        $spread = Get-PerfSelectivitySpread -Items $items -IdProperty 'fid' -Label $areaType.Name
        $field  = $areaType.Field

        $d = Get-PerfSpreadTiers -Name $areaType.Name -Spread $spread `
            -FilterFactory {
                param($id) @{ $field = @("$id") }
            }.GetNewClosure() `
            -MultiFilterFactory {
                param($ider) @{ $field = @($ider | ForEach-Object { "$_" }) }
            }.GetNewClosure()

        if (-not $d) { continue }

        # Mange omraader samtidig er et vanlig brukervalg og gir et helt annet
        # predikat enn ett omraade: en IN-liste i stedet for én verdi.
        #
        # ALDRI MER ENN HALVPARTEN AV DIMENSJONEN
        # Med et fast tak paa 15 valgte fylke:mange ALLE 15 fylkene i Norge.
        # Da var filteret semantisk nesten en no-op - det slapp gjennom
        # 56 685 004 av 56 685 004 fylkesrader - men kostet 1700 ms oppaa
        # gulvet, og caset laa som nummer 1, 2 og 3 paa lista over tregeste
        # kall. Vi maalte og optimaliserte paa noe ingen bruker gjoer: aa velge
        # alle fylker er det samme som aa ikke filtrere.
        #
        # Halvparten holder poenget - en lang IN-liste - uten aa degenerere.
        # Kommuner (357) og verneomraader (3394) paavirkes ikke; det er bare
        # fylke (15) og havomraade (4) som er smaa nok til aa treffe taket.
        $maksMange = [Math]::Min(15, [Math]::Max(2, [int](@($spread.All).Count / 2)))
        $mange = @($spread.All | Select-Object -First $maksMange | ForEach-Object { "$($_.fid)" })
        if ($mange.Count -gt 1) {
            # Noekkelen inneholder BEVISST ikke antallet. Het den "mange-x15" og
            # API-et en dag returnerte faerre omraader, ville noekkelen endret seg
            # og sammenligningen mot baselinen mistet casen.
            $d.tiers = @($d.tiers) + @(New-PerfTier `
                -Key    "$($areaType.Name):mange" `
                -Role   'annen' `
                -Label  "$($mange.Count) tyngste" `
                -Filter @{ $field = $mange })
        }

        $dimensions.Add($d)
        Write-Host ("  {0,-16} {1} nivaa" -f $areaType.Name, $d.tiers.Count)
    }

    # --- Datasett og prosjekt (typeahead) -----------------------------------
    # Endepunktene returnerer OrganizationDto uten observationCount, saa vi kan
    # ikke rangere paa selektivitet. Foerste og siste treff i et bredt soek gir
    # to stabile, men vilkaarlige verdier.
    foreach ($org in @(
        @{ Name = 'datasett'; Path = '/api/Lookup/Datasets?search=a&maxCount=50'; Field = 'datasetOrgId' },
        @{ Name = 'prosjekt'; Path = '/api/Lookup/Projects?search=a&maxCount=50'; Field = 'projectOrgId' })) {

        try { $hits = @(Invoke-PerfApi -Path $org.Path) }
        catch { Write-Warning "Kunne ikke hente $($org.Path): $($_.Exception.Message)"; continue }

        $hits = @($hits | ForEach-Object { $_ } | Where-Object { $null -ne $_.id })
        if ($hits.Count -eq 0) { Write-Warning "Ingen treff for $($org.Name)"; continue }

        $tiers = [System.Collections.Generic.List[object]]::new()
        $tiers.Add((New-PerfTier -Key "$($org.Name):tung" -Role 'tung' `
            -Label $hits[0].name -Filter @{ $($org.Field) = [int]$hits[0].id }))

        if ($hits.Count -gt 1) {
            $tiers.Add((New-PerfTier -Key "$($org.Name):lett" -Role 'lett' `
                -Label $hits[-1].name -Filter @{ $($org.Field) = [int]$hits[-1].id }))
        }

        $d = New-PerfDimension -Name $org.Name -Tiers $tiers
        if ($d) { $dimensions.Add($d); Write-Host ("  {0,-16} {1} nivaa" -f $org.Name, $d.tiers.Count) }
    }

    # --- Katalognummer ------------------------------------------------------
    # Filteret tar IDer fra typeaheaden, ikke strengen. Oppslaget selv maales som
    # eget case i Run.ps1 — gevinsten i filteret er ikke reell hvis kostnaden bare
    # er flyttet dit.
    try {
        $catalog = @(Invoke-PerfApi -Path '/api/Lookup/CatalogNumbers?search=12&maxCount=5' | ForEach-Object { $_ })
        $match   = $catalog | Where-Object { $_.observationIds.Count -gt 0 } | Select-Object -First 1

        if ($match) {
            $d = New-PerfDimension -Name 'katalognr' -Tiers @(
                New-PerfTier -Key 'katalognr:tung' -Role 'tung' `
                    -Label "$($match.catalogNumber) ($($match.observationIds.Count) obs)" `
                    -Filter @{ observationIds = @($match.observationIds) })
            if ($d) { $dimensions.Add($d); Write-Host ("  {0,-16} 1 nivaa" -f 'katalognr') }
        }
    }
    catch { Write-Warning "Kunne ikke hente katalognummer: $($_.Exception.Message)" }

    # --- Dimensjoner med faste verdier --------------------------------------
    # Disse trenger ingen oppslag. Rollene foelger selektivitet: et vidt aarsspenn
    # treffer mye (tung), et smalt treffer lite (lett).
    $dimensions.Add((New-PerfDimension -Name 'regstatus' -Tiers @(
        New-PerfTier -Key 'regstatus:tung'  -Role 'tung'  -Label 'paavist'          -Filter @{ registrationStatusId = 1 }
        New-PerfTier -Key 'regstatus:lett'  -Role 'lett'  -Label 'ikke paavist'     -Filter @{ registrationStatusId = 2 }
        New-PerfTier -Key 'regstatus:annen' -Role 'annen' -Label 'ikke gjenfunnet'  -Filter @{ registrationStatusId = 3 }
    )))

    $dimensions.Add((New-PerfDimension -Name 'bilder' -Tiers @(
        New-PerfTier -Key 'bilder:lett' -Role 'lett' -Label 'med bilder'  -Filter @{ withImages = $true }
        New-PerfTier -Key 'bilder:tung' -Role 'tung' -Label 'uten bilder' -Filter @{ withImages = $false }
    )))

    $dimensions.Add((New-PerfDimension -Name 'periode' -Tiers @(
        New-PerfTier -Key 'periode:tung'  -Role 'tung'  -Label '1970-2026'    -Filter @{ period = @{ from = 1970; to = 2026 } }
        New-PerfTier -Key 'periode:lett'  -Role 'lett'  -Label '2023-2024'    -Filter @{ period = @{ from = 2023; to = 2024 } }
        New-PerfTier -Key 'periode:annen' -Role 'annen' -Label 'sommermnd'    -Filter @{ period = @{ months = @(6, 7, 8) } }
        New-PerfTier -Key 'periode:aapen' -Role 'annen' -Label 'kun fra-aar'  -Filter @{ period = @{ from = 2015 } }
    )))

    $dimensions.Add((New-PerfDimension -Name 'koordpresisjon' -Tiers @(
        New-PerfTier -Key 'koordpresisjon:tung' -Role 'tung' -Label '0-10000 m' -Filter @{ coordinatePrecision = @{ from = 0; to = 10000 } }
        New-PerfTier -Key 'koordpresisjon:lett' -Role 'lett' -Label '0-10 m'    -Filter @{ coordinatePrecision = @{ from = 0; to = 10 } }
    )))

    $fixtures = [PSCustomObject]@{
        createdUtc = (Get-Date).ToUniversalTime().ToString('o')
        baseUrl    = $script:PerfBaseUrl
        dimensions = @($dimensions | Where-Object { $null -ne $_ })
        envelopes  = $script:PerfEnvelopes
    }

    $antallNivaa = ($fixtures.dimensions | ForEach-Object { $_.tiers.Count } | Measure-Object -Sum).Sum
    Write-Host ("  {0} dimensjoner, {1} nivaaer totalt" -f $fixtures.dimensions.Count, $antallNivaa) -ForegroundColor Green

    return $fixtures
}

function Get-PerfFixtureSet {
    <#
        Laster fixtures fra disk, eller bygger dem hvis fila mangler eller
        -Refresh er satt.

        UTSNITTENE FRYSES IKKE
        Frysingen finnes fordi de oppdagede dataene - tyngste institusjon, letteste
        art - kan bytte identitet naar databasen vokser, og da ville sammenligningen
        maalt noe annet enn den tror. Kartutsnittene er ikke oppdaget: de er faste
        konstanter i denne fila. Leses de fra den lagrede JSON-en i stedet, blir en
        endring her stille ignorert til noen kjoerer -RefreshFixtures - som samtidig
        forkaster alle baselinjer. Derfor overstyres de alltid fra $PerfEnvelopes.
    #>
    param(
        [Parameter(Mandatory)] [string] $Path,
        [switch] $Refresh,
        [int]    $TaxonSearchBudget = 80
    )

    if ((Test-Path $Path) -and -not $Refresh) {
        $fixtures = Get-Content $Path -Raw | ConvertFrom-Json
        $antall = ($fixtures.dimensions | ForEach-Object { $_.tiers.Count } | Measure-Object -Sum).Sum
        Write-Host ("Bruker lagrede fixtures fra {0} (laget {1}, {2} dimensjoner, {3} nivaaer)" -f `
            (Split-Path -Leaf $Path), $fixtures.createdUtc, $fixtures.dimensions.Count, $antall) -ForegroundColor DarkGray
        Write-Host '  Kjoer med -RefreshFixtures for aa bygge dem paa nytt (forkaster sammenlignbarhet med gamle baselinjer).' -ForegroundColor DarkGray

        # Samme JSON-runde som resten av fixtures har vaert gjennom, slik at
        # ConvertTo-PerfHashtable ser samme form uansett hvor de kom fra.
        $ferske = $script:PerfEnvelopes | ConvertTo-Json -Depth 6 | ConvertFrom-Json
        $fixtures | Add-Member -NotePropertyName 'envelopes' -NotePropertyValue $ferske -Force
        Write-Host ("  Kartutsnitt fra Fixtures.ps1: {0}" -f $script:PerfEnvelopes.Count) -ForegroundColor DarkGray
        return $fixtures
    }

    $fixtures = New-PerfFixtures -TaxonSearchBudget $TaxonSearchBudget
    $fixtures | ConvertTo-Json -Depth 12 | Set-Content -Path $Path -Encoding utf8
    Write-Host "  Lagret til $(Split-Path -Leaf $Path)" -ForegroundColor DarkGray
    return $fixtures
}
