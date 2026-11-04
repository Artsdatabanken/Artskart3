#Requires -Version 7.0
<#
.SYNOPSIS
    Sammenligner to resultatfiler fra ytelsessuiten og viser regresjoner.

.DESCRIPTION
    Gjoer ingen kall mot API-et og laster ikke databasen - den leser bare to
    CSV-filer. Trygg aa kjoere naar som helst.

    Terskelen er BAADE relativ og absolutt. Bare relativ ville flommet over av
    stoey fra korte case (40 ms -> 90 ms er +125 %, men uinteressant); bare
    absolutt ville skjult at et 300 ms-kall ble tredoblet.

.EXAMPLE
    ./Scripts/PerfSuite/Compare.ps1 -Baseline results/20260901-101500-abc1234-Full.csv `
                                    -Current  results/20260909-084500-bff02c7-Full.csv

.EXAMPLE
    # Uten argumenter: de to nyeste filene i results/
    ./Scripts/PerfSuite/Compare.ps1
#>

[CmdletBinding()]
param(
    [string] $Baseline,
    [string] $Current,

    # Hvor results/ ligger. Tom = utledes, se Get-PerfDataDir.
    [string] $DataDir,

    # Begge terskler maa passeres foer noe rapporteres som regresjon.
    [int]    $MinPercent = 25,
    [int]    $MinMs      = 200
)

$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'PerfLib.psm1') -Force

$resultsDir = Join-Path (Get-PerfDataDir -Override $DataDir) 'results'

if (-not $Baseline -or -not $Current) {
    $filer = @(Get-ChildItem $resultsDir -Filter '*.csv' -ErrorAction SilentlyContinue)

    if ($filer.Count -lt 2) {
        Write-Host 'Trenger minst to resultatfiler i results/ for aa sammenligne automatisk.' -ForegroundColor Red
        Write-Host 'Angi -Baseline og -Current eksplisitt, eller kjoer suiten en gang til.' -ForegroundColor Red
        exit 1
    }

    # Valget ligger i Select-PerfRunPair, med begrunnelsen: den utelukker filen som
    # alt er navngitt, og krever at en auto-valgt baseline er ELDRE enn naa-filen.
    # Den gamle formen kunne sammenligne en fil med seg selv, eller snu
    # sammenligningen slik at forbedringer ble rapportert som regresjoner.
    $par = Select-PerfRunPair -Baseline $Baseline -Current $Current -Filer $filer

    if ($par.Feil) {
        Write-Host $par.Feil -ForegroundColor Red
        Write-Host 'Angi -Baseline og -Current eksplisitt.' -ForegroundColor Red
        exit 1
    }

    $Baseline = $par.Baseline
    $Current  = $par.Current

    Write-Host "Baseline: $(Split-Path -Leaf $Baseline)" -ForegroundColor DarkGray
    Write-Host "Naa:      $(Split-Path -Leaf $Current)"  -ForegroundColor DarkGray
}

# Gjelder ogsaa naar begge er oppgitt eksplisitt: samme fil to ganger gir alltid
# "ingen regresjoner", og det ser ut som et resultat.
if ((Resolve-Path -LiteralPath $Baseline).Path -eq (Resolve-Path -LiteralPath $Current).Path) {
    Write-Host 'Baseline og naa-fil er samme fil. Da sammenlignes den med seg selv.' -ForegroundColor Red
    exit 1
}

Compare-PerfRuns -BaselinePath $Baseline -CurrentPath $Current `
    -MinPercent $MinPercent -MinMs $MinMs
