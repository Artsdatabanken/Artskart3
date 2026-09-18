#Requires -Version 7.0
<#
    Delt bibliotek for ytelsestestene i Scripts/PerfSuite.

    Inneholder det som ellers ville blitt kopiert mellom skriptene: API-kall,
    oppdagelse av testdata (fixtures), maaling av en enkelt case, og
    sammenligning mot en tidligere kjoering.

    De eldre skriptene Scripts/PerfTestAreaCounts.ps1 og Scripts/PerfTestListView.ps1
    har fortsatt sine egne kopier av oppdagelseskoden. De er bevisst latt i fred —
    de fungerer, og en migrering hit hoerer til sin egen endring.
#>

# ---------------------------------------------------------------------------
# API-tilgang
# ---------------------------------------------------------------------------

$script:PerfBaseUrl    = 'https://localhost:5088'
$script:PerfTimeoutSec = 300

# Rangnivaaer vi vil ha testdata for. Speiler de denormaliserte kolonnene pluss
# nivaaene over som loeses opp i minnet av TaxonHierarchyService.
#
# MERK: vanlig hashtable, ikke [ordered]. En ordered dictionary tolker heltalls-
# indeksering som posisjon, ikke noekkel, saa $RankNames[11] ville gitt feil verdi.
$script:RankNames = @{
    1  = 'Kingdom'
    3  = 'Phylum'
    6  = 'Class'
    11 = 'Order'
    15 = 'Family'
    19 = 'Genus'
    22 = 'Species'
}
$script:RankOrder = @(1, 3, 6, 11, 15, 19, 22)

function Get-PerfRankNames { return $script:RankNames }
function Get-PerfRankOrder { return $script:RankOrder }

function Initialize-PerfApi {
    <#
        Setter rot-URL og timeout for resten av modulen. Maa kalles foerst.
    #>
    param(
        [Parameter(Mandatory)] [string] $BaseUrl,
        [int] $TimeoutSec = 300
    )

    $script:PerfBaseUrl    = $BaseUrl.TrimEnd('/')
    $script:PerfTimeoutSec = $TimeoutSec
}

function Invoke-PerfApi {
    param(
        [Parameter(Mandatory)] [string] $Path,
        [string] $Method = 'GET',
        [object] $Body,
        [int]    $TimeoutSec
    )

    # X-CSRF kreves av Duende BFF (Program.cs: .AsBffApiEndpoint()).
    # BFF-sjekken kjoerer foer [AllowAnonymous], saa uten headeren faar vi 401.
    $params = @{
        Uri                  = "$script:PerfBaseUrl$Path"
        Method               = $Method
        Headers              = @{ 'X-CSRF' = '1' }
        SkipCertificateCheck = $true
        TimeoutSec           = if ($TimeoutSec) { $TimeoutSec } else { $script:PerfTimeoutSec }
    }

    if ($null -ne $Body) {
        $params.Body        = ($Body | ConvertTo-Json -Depth 10 -Compress)
        $params.ContentType = 'application/json'
    }

    Invoke-RestMethod @params
}

function Test-PerfApi {
    <#
        Sjekker at API-et svarer foer vi starter en kjoering som tar timer.
        Uten dette produserer suiten hundrevis av "maalinger" som egentlig er
        tilkoblingstimeouts.
    #>
    try {
        $null = Invoke-PerfApi -Path '/api/Lookup/TaxonGroups' -TimeoutSec 30
        return $true
    }
    catch {
        Write-Host ''
        Write-Host "API-et paa $script:PerfBaseUrl svarer ikke: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

function Remove-PerfMarkup {
    # Artstreet returnerer navn med HTML (f.eks. "<i>Larus</i>")
    param([string] $Text)
    if (-not $Text) { return $Text }
    return ($Text -replace '<[^>]+>', '')
}

# ---------------------------------------------------------------------------
# Determinisme
#
# Hele poenget med suiten er at kjoering N kan sammenlignes med kjoering N-1.
# Da maa utvalget av kombinasjoner vaere likt hver gang. Get-Random -SetSeed er
# reproduserbar innenfor én PowerShell-versjon, men ikke garantert paa tvers, saa
# vi bruker en egen LCG i stedet. Den er triviell, men den er vaar.
# ---------------------------------------------------------------------------

function Get-PerfShuffledSample {
    <#
        Deterministisk utvalg av $Count elementer fra $Items.
        Samme seed og samme input gir alltid samme utvalg, i samme rekkefoelge.
    #>
    param(
        [object[]] $Items,
        [int]      $Count,
        [int]      $Seed = 20260909
    )

    $remaining = [System.Collections.Generic.List[object]]::new()
    foreach ($item in @($Items)) { $remaining.Add($item) }

    $picked = [System.Collections.Generic.List[object]]::new()
    $state  = [int64]$Seed
    $n      = [Math]::Min($Count, $remaining.Count)

    for ($i = 0; $i -lt $n; $i++) {
        # LCG med glibc-parametre. Holdes innenfor int64 av modulo-en.
        $state = ([int64]($state * 1103515245 + 12345)) % 2147483648
        if ($state -lt 0) { $state += 2147483648 }

        $index = [int]($state % $remaining.Count)
        $picked.Add($remaining[$index])
        $remaining.RemoveAt($index)
    }

    return $picked.ToArray()
}

# ---------------------------------------------------------------------------
# Oppdagelse av testdata
# ---------------------------------------------------------------------------

function Get-PerfSelectivitySpread {
    <#
        Plukker tyngste, median og letteste element fra en liste med
        observationCount — de tre punktene paa selektivitetsskalaen.

        Dette er kjernen i testen. Et filter mot tyngste institusjon fylles av de
        foerste radene TOP-en leser; et filter mot den letteste kan kreve at
        SQL Server leser store deler av klyngeindeksen foer den finner nok rader.
        Samme kode, samme plan, helt ulik kostnad.

        Mangler alle elementene observationCount, faller vi tilbake til posisjon
        i lista. Da er "tung" og "lett" bare etiketter, men casene blir fortsatt
        stabile mellom kjoeringer, som er det sammenligningen krever.
    #>
    param(
        [object[]] $Items,
        [string]   $IdProperty = 'id',
        [string]   $Label
    )

    # Invoke-RestMethod skriver en JSON-liste som ETT pipeline-objekt, ikke som 54.
    # Ett nivaa utpakking her gjoer funksjonen upaavirket av hvordan kalleren hentet
    # dataene.
    $Items = @($Items | ForEach-Object { $_ })
    $Items = @($Items | Where-Object { $null -ne $_ -and $null -ne $_.$IdProperty })

    if ($Items.Count -eq 0) {
        Write-Warning "Ingen elementer funnet for $Label"
        return $null
    }

    $withCount = @($Items |
        Where-Object { [int64]($_.observationCount ?? 0) -gt 0 } |
        Sort-Object -Property @{ Expression = { [int64]($_.observationCount ?? 0) } } -Descending)

    $hasCounts = $withCount.Count -gt 0
    if (-not $hasCounts) {
        Write-Warning "$Label mangler observationCount - bruker posisjon i lista i stedet"
        # Sorter paa id slik at rekkefoelgen er stabil mellom kjoeringer
        $withCount = @($Items | Sort-Object -Property @{ Expression = { "$($_.$IdProperty)" } })
    }

    $newSample = {
        param($item, $tag)
        [PSCustomObject]@{
            Id    = $item.$IdProperty
            Name  = Remove-PerfMarkup $item.name
            Count = [int64]($item.observationCount ?? 0)
            Tag   = $tag
        }
    }

    return [PSCustomObject]@{
        Heaviest  = & $newSample $withCount[0] 'tung'
        Median    = & $newSample $withCount[[int]($withCount.Count / 2)] 'median'
        Lightest  = & $newSample $withCount[-1] 'lett'

        # RAA liste, i synkende rekkefoelge. Elementene har feltnavnene
        # oppslagsendepunktet brukte - 'id' for de fleste, 'fid' for omraader.
        All       = $withCount

        # NORMALISERT liste, samme rekkefoelge. Bruk denne naar du trenger
        # id-en uten aa vite hvilket felt den kom fra.
        #
        # Uten den bommet flerverdi-nivaaene for alle fire omraadetypene: de
        # leste $_.Id paa raa-objektene, som for omraader er Area.Id og ikke Fid.
        # Filteret ble {"municipalityIds":["807"]} der det skulle staa ["4206"] -
        # en verdi som ikke finnes, altsaa et filter som maaler ingenting.
        AllSamples = @($withCount | ForEach-Object { & $newSample $_ 'annen' })

        HasCounts = $hasCounts
    }
}

function Get-PerfTaxonSamples {
    <#
        Finner tyngste og letteste takson per rangnivaa via best-foerst-soek i artstreet.

        Et graadig dypdykk (alltid tyngste barn) ville bare gitt tyngste takson langs
        én gren. Den tyngste arten ligger ikke noedvendigvis under den tyngste
        ordenen — den kan sitte i en helt annen del av treet. Derfor ekspanderer vi
        alltid den tyngste ikke-besoekte noden paa tvers av hele fronten, og beholder
        maks og min per rangnivaa underveis.

        Minimum er tatt med fordi listevisningen er treg for LETTE filtre, ikke
        tunge: TOP-en maa lese langt ned i klyngeindeksen foer den er fylt.
    #>
    param(
        [int] $MaxRequests = 60,
        [int] $FrontierCap = 250
    )

    Write-Host "  Soeker i artstreet (maks $MaxRequests kall)..." -ForegroundColor DarkGray

    $heavy    = @{}
    $light    = @{}
    $frontier = [System.Collections.Generic.List[object]]::new()
    $requests = 0

    $getWeight = { param($node) [int64]($node.cumulativeObservationCount ?? 0) }

    $register = {
        param($node)
        # Eksplisitt [int]-cast: JSON-tall kan deserialiseres som Int64,
        # som ikke matcher Int32-noeklene i $RankNames.
        $rank = [int]$node.taxonRankId
        if (-not $script:RankNames.ContainsKey($rank)) { return }

        $weight = [int64]($node.cumulativeObservationCount ?? 0)
        if ($weight -le 0) { return }

        $sample = [PSCustomObject]@{
            Id    = [int]$node.id
            Name  = Remove-PerfMarkup ($node.validScientificName ?? $node.preferredPopularName)
            Count = $weight
        }

        if (-not $heavy.ContainsKey($rank) -or $heavy[$rank].Count -lt $weight) { $heavy[$rank] = $sample }
        if (-not $light.ContainsKey($rank) -or $light[$rank].Count -gt $weight) { $light[$rank] = $sample }
    }

    try { $roots = Invoke-PerfApi -Path '/api/Lookup/TaxonTree' }
    catch {
        Write-Warning "Kunne ikke hente artstreet: $($_.Exception.Message)"
        return @{ Heavy = $heavy; Light = $light }
    }

    foreach ($root in $roots) {
        & $register $root
        if ($root.hasChildren) { $frontier.Add($root) }
    }

    while ($frontier.Count -gt 0 -and $requests -lt $MaxRequests) {
        $sorted  = $frontier | Sort-Object -Property @{ Expression = { & $getWeight $_ } } -Descending
        $current = $sorted[0]
        $frontier.Remove($current) | Out-Null

        try { $children = Invoke-PerfApi -Path "/api/Lookup/TaxonTree?parentTaxonId=$($current.id)" }
        catch { continue }
        finally { $requests++ }

        if (-not $children) { continue }

        foreach ($child in $children) {
            & $register $child
            if ($child.hasChildren) { $frontier.Add($child) }
        }

        # Hold fronten liten — vi bryr oss uansett bare om de tyngste grenene
        if ($frontier.Count -gt $FrontierCap) {
            $trimmed = $frontier |
                Sort-Object -Property @{ Expression = { & $getWeight $_ } } -Descending |
                Select-Object -First $FrontierCap
            $frontier.Clear()
            foreach ($n in $trimmed) { $frontier.Add($n) }
        }
    }

    Write-Host "  ($requests kall, $($heavy.Count) rangnivaa funnet)" -ForegroundColor DarkGray
    return @{ Heavy = $heavy; Light = $light }
}

# ---------------------------------------------------------------------------
# Maaling
# ---------------------------------------------------------------------------

function Measure-PerfRequest {
    <#
        Kjoerer ett kall $Runs ganger og returnerer kald tid, varm median og radtall.

        Foerste kjoering holdes utenfor medianen. Den betaler for plankompilering i
        SQL Server og for lesing fra disk, mens de neste treffer plancache og
        bufferpool. Begge tallene er interessante: brukeren moeter den kalde tiden
        naar filteret er nytt, og den varme naar hun blar videre.
    #>
    param(
        [Parameter(Mandatory)] [string] $Path,
        [string]   $Method = 'POST',
        [object]   $Body,
        [int]      $Runs = 2,
        [int]      $TimeoutSec = 60
    )

    $times  = [System.Collections.Generic.List[int]]::new()
    $status = 'OK'
    $rows   = 0

    for ($i = 0; $i -lt $Runs; $i++) {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        try {
            $response = Invoke-PerfApi -Path $Path -Method $Method -Body $Body -TimeoutSec $TimeoutSec

            # Responsformene varierer mellom endepunktene, saa radtellingen er tilnaermet.
            $rows = if ($null -eq $response)     { 0 }
                    elseif ($response.locations) { @($response.locations).Count }
                    elseif ($response.items)     { @($response.items).Count }
                    else                         { @($response).Count }
        }
        catch {
            $sw.Stop()
            # En timeout er ikke en maaling. Vi skiller den fra andre feil fordi den
            # betyr "tregere enn taket", ikke "virker ikke".
            $status = if ($_.Exception.Message -match 'timed out|timeout|cancel') {
                          "TIMEOUT >${TimeoutSec}s"
                      } else {
                          "FEIL: $($_.Exception.Message)"
                      }
            break
        }
        $sw.Stop()
        $times.Add([int]$sw.Elapsed.TotalMilliseconds)
    }

    # Ved timeout finnes ingen maaling. Vi setter taket som tid slik at casen
    # sorterer oeverst i rapporten, men Status skiller den fra en ekte maaling.
    $cold = if ($times.Count -gt 0) { $times[0] } else { $TimeoutSec * 1000 }
    $warm = @($times | Select-Object -Skip 1)
    $median = if ($warm.Count -gt 0) {
                  $sorted = @($warm | Sort-Object)
                  $sorted[[int]($sorted.Count / 2)]
              } else { $cold }

    return [PSCustomObject]@{
        ColdMs = $cold
        WarmMs = $median
        Rows   = $rows
        Status = $status
    }
}

# ---------------------------------------------------------------------------
# Sammenligning mot tidligere kjoering
# ---------------------------------------------------------------------------

function Compare-PerfRuns {
    <#
        Sammenligner to resultatfiler og skriver ut regresjoner og forbedringer.

        Terskelen er BAADE relativ og absolutt. Bare relativ ville flommet over av
        stoey fra korte case (40 ms -> 90 ms er +125 %, men uinteressant); bare
        absolutt ville skjult at et 300 ms-kall ble tredoblet.

        Noekkelen er CaseKey + Endpoint + Variant. Den er avledet av dimensjon og
        selektivitetsnivaa, ikke av navnet paa det oppslaatte elementet, slik at
        sammenligningen holder selv om "tyngste institusjon" bytter identitet.
    #>
    param(
        [Parameter(Mandatory)] [string] $BaselinePath,
        [Parameter(Mandatory)] [string] $CurrentPath,
        [int]    $MinPercent = 25,
        [int]    $MinMs      = 200
    )

    if (-not (Test-Path $BaselinePath)) { Write-Host "Fant ikke baseline: $BaselinePath" -ForegroundColor Red; return }
    if (-not (Test-Path $CurrentPath))  { Write-Host "Fant ikke resultatfil: $CurrentPath" -ForegroundColor Red; return }

    $baseline = @(Import-Csv $BaselinePath | Where-Object Status -eq 'OK')
    $current  = @(Import-Csv $CurrentPath  | Where-Object Status -eq 'OK')

    $baseIndex = @{}
    foreach ($row in $baseline) { $baseIndex["$($row.CaseKey)|$($row.Endpoint)|$($row.Variant)"] = $row }

    $currentKeys = [System.Collections.Generic.HashSet[string]]::new()
    $deltas      = [System.Collections.Generic.List[object]]::new()
    $nyeCase     = 0

    foreach ($row in $current) {
        $key = "$($row.CaseKey)|$($row.Endpoint)|$($row.Variant)"
        [void]$currentKeys.Add($key)

        if (-not $baseIndex.ContainsKey($key)) { $nyeCase++; continue }

        $before = [int]$baseIndex[$key].WarmMs
        $after  = [int]$row.WarmMs
        $diff   = $after - $before
        $pct    = if ($before -gt 0) { [int][Math]::Round(100.0 * $diff / $before) } else { 0 }

        # Byttet planen indeks? Det skiller «mer data eller travel maskin» fra
        # «optimalisereren valgte noe annet», som er to helt ulike funn.
        $indeksEndring = Compare-PerfIndexUsage -Before $baseIndex[$key].Indexes -After $row.Indexes

        $deltas.Add([PSCustomObject]@{
            Case      = $row.CaseKey
            Endepunkt = "$($row.Endpoint) $($row.Variant)"
            FoerMs    = $before
            EtterMs   = $after
            DiffMs    = $diff
            Pst       = $pct
            Indeks    = $indeksEndring
        })
    }

    Write-Host "`n$('=' * 78)"
    Write-Host "Sammenligning mot $(Split-Path -Leaf $BaselinePath)" -ForegroundColor Green
    Write-Host ("  {0} case sammenlignet, {1} nye. Terskel: >{2} % OG >{3} ms" -f `
        $deltas.Count, $nyeCase, $MinPercent, $MinMs)

    $regresjoner  = @($deltas | Where-Object { $_.DiffMs -ge $MinMs  -and $_.Pst -ge $MinPercent }  | Sort-Object DiffMs -Descending)
    $forbedringer = @($deltas | Where-Object { $_.DiffMs -le -$MinMs -and $_.Pst -le -$MinPercent } | Sort-Object DiffMs)

    if ($regresjoner.Count -gt 0) {
        Write-Host "`n$($regresjoner.Count) REGRESJONER:" -ForegroundColor Red
        $regresjoner | Select-Object -First 30 | Format-Table -AutoSize

        # Planvipping foerst: det er den kategorien som peker paa en indeks- eller
        # statistikkendring, ikke paa at det er blitt mer data.
        $byttetPlan = @($regresjoner | Where-Object { $_.Indeks -notin @('samme', 'ukjent', '') })
        if ($byttetPlan.Count -gt 0) {
            Write-Host ("  {0} av {1} regresjoner byttet indeks - se Indeks-kolonnen." -f `
                $byttetPlan.Count, $regresjoner.Count) -ForegroundColor Yellow

            Write-Host '  Hyppigste planendringer:' -ForegroundColor DarkGray
            $byttetPlan | Group-Object Indeks | Sort-Object Count -Descending |
                Select-Object -First 5 | ForEach-Object {
                    Write-Host ("    {0,4}x  {1}" -f $_.Count, $_.Name) -ForegroundColor DarkGray
                }
        }
    }
    else {
        Write-Host "`nIngen regresjoner over terskelen." -ForegroundColor Green
    }

    if ($forbedringer.Count -gt 0) {
        Write-Host "$($forbedringer.Count) forbedringer:" -ForegroundColor Green
        $forbedringer | Select-Object -First 15 | Format-Table -AutoSize
    }

    $manglende = @($baseIndex.Keys | Where-Object { -not $currentKeys.Contains($_) })
    if ($manglende.Count -gt 0) {
        Write-Host ("{0} case fra baseline mangler her (avbrutt kjoering, annet -Level, eller endret matrise)." -f `
            $manglende.Count) -ForegroundColor Yellow
    }
}

function Get-PerfDimensionCost {
    <#
        Marginalkostnad per filterdimensjon: snitt varm tid for alle case SOM
        inneholder dimensjonen, minus snittet for alle som ikke gjoer det.

        Dette er analysen enkeltmaalingene ikke kan gi. Med et par hundre
        kombinasjoner kan vi si hvilken dimensjon som faktisk koster, i stedet
        for aa lese topp-15-lista og gjette.

        Tallet er en korrelasjon, ikke et bevis: dimensjoner som ofte opptrer
        sammen faar liknende skaar. Bruk det til aa velge hva som skal
        undersoekes naermere, ikke som konklusjon.
    #>
    param([object[]] $Results)

    $ok = @($Results | Where-Object Status -eq 'OK')
    if ($ok.Count -eq 0) { return @() }

    $alleDimensjoner = @($ok | ForEach-Object { $_.Dimensions -split ',' } |
        Where-Object { $_ } | Sort-Object -Unique)

    $rader = foreach ($dim in $alleDimensjoner) {
        $med  = @($ok | Where-Object { ($_.Dimensions -split ',') -contains $dim })
        $uten = @($ok | Where-Object { ($_.Dimensions -split ',') -notcontains $dim })
        if ($med.Count -eq 0 -or $uten.Count -eq 0) { continue }

        $snittMed  = ($med  | Measure-Object WarmMs -Average).Average
        $snittUten = ($uten | Measure-Object WarmMs -Average).Average

        [PSCustomObject]@{
            Dimensjon   = $dim
            Case        = $med.Count
            SnittMedMs  = [int]$snittMed
            SnittUtenMs = [int]$snittUten
            MarginalMs  = [int]($snittMed - $snittUten)
            MaksMs      = [int](($med | Measure-Object WarmMs -Maximum).Maximum)
        }
    }

    return @($rader | Sort-Object MarginalMs -Descending)
}

# ---------------------------------------------------------------------------
# Indeksbruk per case
#
# HVORFOR
# Uten dette kan en kjoering fortelle AT noe ble tregere, men ikke HVORFOR.
# Det er to helt ulike funn som ser like ut i en ren tidssammenligning:
#
#   samme indeks, lengre tid   - mer data, eller en maskin under last
#   ANNEN indeks, lengre tid   - planvipping
#
# Det andre er det som er verdt en alarm. En ny dekkende indeks kan gjoere den
# sorterte scanen billig nok til at optimalisereren velger den - og for filtre
# som er spredte EXISTS-semi-joins er den katastrofal. Det skjedde 11.09.2026:
# IX_Observation_ListView ga 142 forbedringer og 202 regresjoner, og
# regresjonene var nesten alle case der planen byttet til den nye indeksen.
#
# sys.dm_db_index_usage_stats teller kumulativt. Suiten kjoerer casene serielt,
# saa en differanse foer og etter hver maaling attribuerer bruken presist.
#
# MERK: tellerne nullstilles ved omstart av SQL Server og ved gjenoppbygging av
# en indeks. Vi leser derfor alltid utgangsverdien, og antar aldri null.
#
# Lesningen skjer ETTER at stoppeklokken er stoppet, saa den aldri havner i
# maaletallet.
# ---------------------------------------------------------------------------

$script:PerfSqlConnection = $null

# Bare foerste feil rapporteres — se catch-blokken i Get-PerfIndexSnapshot.
$script:PerfIndexWarned = $false

# Tabellene soekestien faktisk roerer. Avgrensningen er ikke bare for aa holde
# resultatet lesbart - den er der for kostnaden.
$script:PerfIndexTables = @(
    'dbo.Observation', 'dbo.ObservationEntityIndex', 'dbo.ObservationTaxonHierarchy',
    'dbo.ObservationTags', 'dbo.ObservationBehaviors', 'dbo.ObservationProject',
    'dbo.MediaFile', 'dbo.Location', 'dbo.LocationAreas', 'dbo.Taxon',
    'dbo.Organization', 'dbo.Area'
)

# Bygges av Initialize-PerfSql, med object_id-ene som LITERALER.
#
# Maalt paa 98 rader: OBJECT_ID('dbo.X') i IN-lista tok 144 ms, de samme
# id-ene som tall tok 2 ms. Funksjonen evalueres per rad, og det var hele
# kostnaden. Med to snapshots per maaling og 3556 maalinger er forskjellen
# 14 minutter mot et kvarters sekunder paa en kjoering som allerede tar
# nesten to timer.
$script:PerfIndexUsageSql = $null

function Initialize-PerfSql {
    <#
        Aapner én vedvarende forbindelse for DMV-lesningene. Uten den ville hver
        maaling kostet en sqlcmd-prosess - 6608 prosessoppstarter i en full
        kjoering.

        Returnerer $false hvis forbindelsen ikke lar seg aapne. Da kjoerer suiten
        videre uten indekssporing; det er en diagnose, ikke en forutsetning.
    #>
    param(
        [Parameter(Mandatory)] [string] $Server,
        [Parameter(Mandatory)] [string] $Database
    )

    try {
        $cs = "Data Source=$Server;Initial Catalog=$Database;Integrated Security=True;" +
              "TrustServerCertificate=True;Connect Timeout=15"
        $conn = [System.Data.SqlClient.SqlConnection]::new($cs)
        $conn.Open()
        $script:PerfSqlConnection = $conn

        # Slaa opp object_id-ene én gang og bygg spoerringen med literaler.
        # Tabeller som ikke finnes gir NULL og utelates - da maaler vi bare de
        # som er der, i stedet for aa feile paa et miljoe uten dem.
        $navnListe = (@($script:PerfIndexTables | ForEach-Object { "OBJECT_ID('$_')" }) -join ', ')
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = "SELECT $navnListe"
        $reader = $cmd.ExecuteReader()

        $ider = @()
        if ($reader.Read()) {
            for ($i = 0; $i -lt $reader.FieldCount; $i++) {
                if (-not $reader.IsDBNull($i)) { $ider += $reader.GetInt32($i) }
            }
        }
        $reader.Dispose(); $cmd.Dispose()

        if ($ider.Count -eq 0) {
            Write-Warning 'Fant ingen av tabellene indekssporingen ser etter.'
            $script:PerfSqlConnection = $null
            $conn.Close()
            return $false
        }

        $script:PerfIndexUsageSql = @"
SELECT i.name AS IndexName,
       ISNULL(s.user_seeks, 0)   AS Seeks,
       ISNULL(s.user_scans, 0)   AS Scans,
       ISNULL(s.user_lookups, 0) AS Lookups
FROM sys.indexes i
LEFT JOIN sys.dm_db_index_usage_stats s
       ON s.object_id = i.object_id AND s.index_id = i.index_id
      AND s.database_id = DB_ID()
WHERE i.name IS NOT NULL AND i.object_id IN ($($ider -join ', '))
"@

        return $true
    }
    catch {
        Write-Warning "Kunne ikke koble til $Server/$Database for indekssporing: $($_.Exception.Message)"
        $script:PerfSqlConnection = $null
        return $false
    }
}

function Close-PerfSql {
    if ($script:PerfSqlConnection) {
        $script:PerfSqlConnection.Close()
        $script:PerfSqlConnection.Dispose()
        $script:PerfSqlConnection = $null
    }
}

function Get-PerfIndexSnapshot {
    <#
        Oejeblikksbilde av tellerne, som hashtable: indeksnavn -> [seeks, scans, lookups].
        Returnerer $null naar sporing ikke er aktiv, slik at kalleren kan hoppe over.
    #>
    if (-not $script:PerfSqlConnection) { return $null }

    # try/finally rundt leseren, ikke bare try/catch.
    #
    # Uten finally blir en leser som feiler midt i uthentingen staaende aapen paa
    # forbindelsen, og siden MultipleActiveResultSets ikke er paa, feiler ALLE
    # paafoelgende kall med «There is already an open DataReader». Én feil ble
    # dermed til 3556 feil, og sporingen var stille av resten av kjoeringen.
    $cmd = $null
    $reader = $null

    try {
        $cmd = $script:PerfSqlConnection.CreateCommand()
        $cmd.CommandText = $script:PerfIndexUsageSql
        $cmd.CommandTimeout = 30
        $reader = $cmd.ExecuteReader()

        $snapshot = @{}
        while ($reader.Read()) {
            # Samme indeksnavn kan finnes paa flere tabeller. Navnet er likevel
            # noekkelen vi vil lese i rapporten, saa vi summerer i stedet for aa
            # kvalifisere med tabellnavn og gjoere kolonnen uleselig.
            #
            # Navngitte felter, ikke en array per indeks: med array ble
            # $snapshot[$navn][0] tolket som indeksering paa hashtable-treffet
            # heller enn paa arrayen, og summeringen feilet med «Object[] does not
            # contain a method named op_Addition».
            $navn = $reader.GetString(0)
            $s = [int64]$reader.GetValue(1)
            $c = [int64]$reader.GetValue(2)
            $l = [int64]$reader.GetValue(3)

            $rad = $snapshot[$navn]
            if ($null -ne $rad) {
                $rad.Seeks   += $s
                $rad.Scans   += $c
                $rad.Lookups += $l
            }
            else {
                $snapshot[$navn] = [PSCustomObject]@{ Seeks = $s; Scans = $c; Lookups = $l }
            }
        }
        return $snapshot
    }
    catch {
        # Bare foerste feil rapporteres. Er aarsaken vedvarende, ville en advarsel
        # per maaling gitt tusenvis av linjer og begravd selve maaleresultatet.
        if (-not $script:PerfIndexWarned) {
            Write-Warning "Indekssporing feilet, fortsetter uten: $($_.Exception.Message)"
            $script:PerfIndexWarned = $true
        }
        return $null
    }
    finally {
        if ($reader) { $reader.Dispose() }
        if ($cmd)    { $cmd.Dispose() }
    }
}

function Get-PerfIndexDelta {
    <#
        Differansen mellom to oejeblikksbilder, som én kompakt streng egnet for
        en CSV-kolonne:  IX_Navn:s12/c1/l40;Annen:s3

        Bare indekser med endring tas med, sortert paa total aktivitet. Listen
        kuttes ved $MaxIndexes - hensikten er aa se HVILKEN plan som ble valgt,
        ikke aa foere fullstendig regnskap.
    #>
    param(
        [hashtable] $Before,
        [hashtable] $After,
        [int]       $MaxIndexes = 6
    )

    if ($null -eq $Before -or $null -eq $After) { return '' }

    $endringer = foreach ($navn in $After.Keys) {
        $f = $Before[$navn]
        $e = $After[$navn]

        $s = $e.Seeks   - $(if ($f) { $f.Seeks }   else { 0 })
        $c = $e.Scans   - $(if ($f) { $f.Scans }   else { 0 })
        $l = $e.Lookups - $(if ($f) { $f.Lookups } else { 0 })

        # Negativ differanse betyr at tellerne ble nullstilt underveis - typisk
        # en indeksgjenoppbygging. Da er tallet meningsloest, ikke null.
        if ($s -lt 0 -or $c -lt 0 -or $l -lt 0) { continue }
        if (($s + $c + $l) -eq 0) { continue }

        [PSCustomObject]@{ Navn = $navn; Seeks = $s; Scans = $c; Lookups = $l; Total = $s + $c + $l }
    }

    $valgte = @($endringer | Sort-Object Total -Descending | Select-Object -First $MaxIndexes)
    if ($valgte.Count -eq 0) { return '' }

    return (@($valgte | ForEach-Object {
        $deler = @()
        if ($_.Seeks   -gt 0) { $deler += "s$($_.Seeks)" }
        if ($_.Scans   -gt 0) { $deler += "c$($_.Scans)" }
        if ($_.Lookups -gt 0) { $deler += "l$($_.Lookups)" }
        "$($_.Navn):$($deler -join '/')"
    }) -join ';')
}

function Compare-PerfIndexUsage {
    <#
        Skiller regresjoner der PLANEN byttet fra dem der den ikke gjorde det.
        Sammenligner bare indeksNAVNENE, ikke tellerne - det er hvilke indekser
        som ble roert som forteller om planen er en annen.
    #>
    param([string] $Before, [string] $After)

    $navnFoer  = @(($Before -split ';') | ForEach-Object { ($_ -split ':')[0] } | Where-Object { $_ } | Sort-Object -Unique)
    $navnEtter = @(($After  -split ';') | ForEach-Object { ($_ -split ':')[0] } | Where-Object { $_ } | Sort-Object -Unique)

    if ($navnFoer.Count -eq 0 -or $navnEtter.Count -eq 0) { return 'ukjent' }
    if (($navnFoer -join ',') -eq ($navnEtter -join ',')) { return 'samme' }

    $nye = @($navnEtter | Where-Object { $_ -notin $navnFoer })
    $borte = @($navnFoer | Where-Object { $_ -notin $navnEtter })

    $deler = @()
    if ($nye.Count -gt 0)   { $deler += "+$($nye -join ',')" }
    if ($borte.Count -gt 0) { $deler += "-$($borte -join ',')" }
    return ($deler -join ' ')
}

Export-ModuleMember -Function `
    Get-PerfRankNames, Get-PerfRankOrder, `
    Initialize-PerfApi, Invoke-PerfApi, Test-PerfApi, Remove-PerfMarkup, `
    Get-PerfShuffledSample, Get-PerfSelectivitySpread, Get-PerfTaxonSamples, `
    Measure-PerfRequest, Compare-PerfRuns, Get-PerfDimensionCost, `
    Initialize-PerfSql, Close-PerfSql, Get-PerfIndexSnapshot, `
    Get-PerfIndexDelta, Compare-PerfIndexUsage
