#Requires -Version 7.0
<#
    Stubbe for Kartverkets Eiendom API.

    Dot-sources av Run.ps1, Record-Kartverket.ps1 og Test-Matrix.ps1. Krever at
    PerfLib er importert foerst.

    HVORFOR
    Matrikkelfilteret slaar opp omrisset til eiendommene hos Kartverket foer
    spoerringen bygges. Mot den ekte tjenesten ville maalingene avhengt av nettet,
    av at tjenesten svarer, og av Matrikkelen, som oppdateres hver natt. Stubben
    svarer med opptak fra Kartverket/, saa det er bare nettverkskallet som byttes
    ut: parsingen, sammenslaaingen av teigene, STIntersects og bufringen i
    CadastralUnitService kjoerer som i produksjon.

    API-et maa startes med

        KartverketEiendom__BaseUrl=http://localhost:5099/

    Run.ps1 sjekker det foer noe maales, med et matrikkelnummer bare stubben kjenner.

    Stubben svarer 500 paa et nummer den ikke har opptak av, i stedet for en tom
    samling slik Kartverket gjoer. Et glemt opptak skal gi feilede maalinger, ikke
    stille maale et filter uten treff.
#>

$script:PerfKartverketDir = Join-Path $PSScriptRoot 'Kartverket'

# Kartverket kjenner ikke kommunenummer 9999. Svarer API-et 404 paa dette, spoer
# det den ekte tjenesten og ikke stubben.
$script:PerfKartverketSentinel = '9999-1/1'
$script:PerfKartverketSentinelSvar = '{"type":"FeatureCollection","features":[{"type":"Feature","geometry":{"type":"Polygon","coordinates":[[[0,0],[1,0],[1,1],[0,0]]]},"properties":{}}]}'

$script:PerfKartverketListener = $null
$script:PerfKartverketJobName  = 'PerfKartverketStub'

function Get-PerfKartverketFile {
    <#
        Opptaksfila for et matrikkelnummer. Skraastrek er ikke lov i filnavn, saa
        0301-147/378 lagres som 0301-147_378.json.
    #>
    param([Parameter(Mandatory)] [string] $Id)

    return Join-Path $script:PerfKartverketDir (($Id -replace '/', '_') + '.json')
}

function Start-PerfKartverketStub {
    <#
        Starter stubben i en traadjobb i denne prosessen. Returnerer $false hvis
        porten ikke kan tas.
    #>
    param([int] $Port = 5099)

    # En avbrutt kjoering i samme sesjon kan ha latt en stubbe ligge igjen.
    Stop-PerfKartverketStub

    # Leses inn foer start, saa stubben ikke leser fra disk mens det maales.
    $svar = @{}
    foreach ($fil in @(Get-ChildItem $script:PerfKartverketDir -Filter '*.json' -ErrorAction SilentlyContinue)) {
        $svar[$fil.BaseName -replace '_', '/'] = [System.IO.File]::ReadAllBytes($fil.FullName)
    }
    $svar[$script:PerfKartverketSentinel] = [System.Text.Encoding]::UTF8.GetBytes($script:PerfKartverketSentinelSvar)

    $listener = [System.Net.HttpListener]::new()
    $listener.Prefixes.Add("http://localhost:$Port/")
    try {
        $listener.Start()
    }
    catch {
        Write-Host "Kartverket-stubben fikk ikke port ${Port}: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
    $script:PerfKartverketListener = $listener

    $null = Start-ThreadJob -Name $script:PerfKartverketJobName -ArgumentList $listener, $svar -ScriptBlock {
        param($listener, $svar)

        try {
            while ($listener.IsListening) {
                # Wait med tak, ikke GetContext(): et blokkerende kall kan ikke
                # avbrytes av Stop-Job.
                $venter = $listener.GetContextAsync()
                while (-not $venter.Wait(250)) { }
                $ctx = $venter.Result

                try {
                    $id = $ctx.Request.QueryString['matrikkelnummer']
                    if ($ctx.Request.Url.AbsolutePath -eq '/geokoding' -and $id -and $svar.ContainsKey($id)) {
                        $ctx.Response.StatusCode = 200
                        $body = $svar[$id]
                    }
                    else {
                        $ctx.Response.StatusCode = 500
                        $body = [System.Text.Encoding]::UTF8.GetBytes("Kartverket-stubben har ikke opptak av '$id'. Kjoer Record-Kartverket.ps1.")
                    }

                    $ctx.Response.ContentType = 'application/json; charset=utf-8'
                    $ctx.Response.OutputStream.Write($body, 0, $body.Length)
                    $ctx.Response.Close()
                }
                catch {
                    # Et svar som ikke kan skrives, f.eks. fordi API-et har brutt
                    # forbindelsen, skal ikke stoppe stubben for resten av kjoeringen.
                    $ctx.Response.Abort()
                }
            }
        }
        catch {
            # Stop() paa lytteren avbryter ventingen med et unntak. Det er slik
            # loekka avsluttes.
        }
        finally {
            $listener.Close()
        }
    }

    return $true
}

function Stop-PerfKartverketStub {
    if ($script:PerfKartverketListener) {
        $script:PerfKartverketListener.Stop()
        $script:PerfKartverketListener = $null
    }
    Get-Job -Name $script:PerfKartverketJobName -ErrorAction SilentlyContinue | Remove-Job -Force
}

function Test-PerfKartverketStub {
    <#
        Sjekker at API-et faktisk spoer stubben, ved aa slaa opp et nummer bare
        stubben kjenner. Uten denne ville en kjoering mot et API uten
        KartverketEiendom__BaseUrl maalt den ekte tjenesten i stillhet.
    #>
    param([int] $Port = 5099)

    $id = [uri]::EscapeDataString($script:PerfKartverketSentinel)
    try {
        $null = Invoke-PerfApi -Path "/api/Lookup/CadastralUnit?id=$id" -TimeoutSec 30
        return $true
    }
    catch {
        $status = [int]$_.Exception.Response.StatusCode
        $hvorfor = switch ($status) {
            404     { 'API-et spoer den ekte Kartverket-tjenesten, ikke stubben.' }
            502     { 'API-et naar ikke stubben. Peker det paa en annen port?' }
            default { $_.Exception.Message }
        }
        Write-Host "Kartverket-stubben er ikke i bruk: $hvorfor" -ForegroundColor Red
        Write-Host "Start API-et med KartverketEiendom__BaseUrl=http://localhost:$Port/ og proev igjen." -ForegroundColor Red
        return $false
    }
}
