#Requires -Version 5.1
param([Parameter(Mandatory=$true)][string]$ExePath)
$ErrorActionPreference='Stop'
$previousExe=$env:KS_EXE
$previousRoot=$env:KILLERSCAN_DATA_ROOT
$previousLocation=$env:KS_LOCATION_ID
try {
    $env:KS_EXE=(Resolve-Path -LiteralPath $ExePath).Path
    $env:KILLERSCAN_DATA_ROOT=Join-Path ([IO.Path]::GetTempPath()) ('KillerScan-evidence-module-'+[guid]::NewGuid().ToString('N'))
    $env:KS_LOCATION_ID=$null
    Import-Module (Join-Path $PSScriptRoot '../Modules/KillerScan/KillerScan.psm1') -Force
    # /demo is a CLI flag, never a real target: no scanning or network settings changes.
    $devices=@(scan '/demo')
    if (!$devices.Count) { throw 'The actual GUI EXE returned no JSON through the module.' }
    if (@($devices | Where-Object { @($_.Access).Count -gt 0 }).Count -eq 0) { throw 'Access endpoints were lost in the actual module pipeline.' }
    if (@($devices | Where-Object { $_.HomeAssistant.RecommendedDomain }).Count -eq 0) { throw 'The fabricated supported example lost its direct HA choice.' }
    foreach ($device in $devices) {
        if (!$device.Identification -or !$device.Identification.AutomaticType) { throw 'Recognition data was lost in the actual module pipeline.' }
        if ($device.Identification.ObservationCompletedAtUtc) { throw 'Demo invented a live capture time.' }
        if (!$device.HomeAssistant -or !$device.HomeAssistant.CatalogueRevision -or !$device.HomeAssistant.Steps.Count) {
            throw 'Home Assistant advice was lost in the actual module pipeline, including unknown devices.'
        }
        foreach ($candidate in $device.HomeAssistant.Candidates) {
            foreach ($fact in $candidate.Evidence) {
                if ($fact.ObservedAtUtc) { throw 'HA demo advice invented a live capture time.' }
            }
        }
    }
    $refused=$false
    try { probe '192.0.2.10' -LocationId 'not-a-location' | Out-Null }
    catch { $refused=$true }
    if (!$refused) { throw 'An invalid location did not propagate a CLI usage error before probing.' }
    Write-Output ('PASS: actual PowerShell module/GUI EXE round trip, '+$devices.Count+' fabricated assessments and usage-error propagation; no scan.')
}
finally {
    $env:KS_EXE=$previousExe
    $env:KILLERSCAN_DATA_ROOT=$previousRoot
    $env:KS_LOCATION_ID=$previousLocation
}
