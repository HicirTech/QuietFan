<#
.SYNOPSIS
    Installs QuietFan silently, checks the files and registry entries, then uninstalls and checks the cleanup.
.DESCRIPTION
    Meant for CI runners: it installs into the current user's profile and changes the user's Run key.
#>
[CmdletBinding()]
param(
    [string]$Setup = (Get-ChildItem (Join-Path $PSScriptRoot '..\dist') -Filter 'QuietFan-Setup-*.exe' | Select-Object -First 1).FullName
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$appExe = Join-Path $env:LOCALAPPDATA 'Programs\QuietFan\QuietFan.exe'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall'
$silent = '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART'

function Assert-That([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "FAILED: $Message" }
    Write-Host "ok   $Message"
}

function Test-RunValue {
    $values = Get-ItemProperty $runKey -ErrorAction SilentlyContinue
    return $null -ne $values -and $null -ne $values.PSObject.Properties['QuietFan']
}

function Test-UninstallEntry {
    foreach ($key in Get-ChildItem $uninstallKey -ErrorAction SilentlyContinue) {
        $name = (Get-ItemProperty $key.PSPath).PSObject.Properties['DisplayName']
        if ($null -ne $name -and $name.Value -eq 'QuietFan') { return $true }
    }
    return $false
}

Assert-That (Test-Path $Setup) "installer found: $Setup"

$install = Start-Process $Setup -ArgumentList ($silent + '/TASKS=autostart') -Wait -PassThru
Assert-That ($install.ExitCode -eq 0) "installer exit code is 0 (got $($install.ExitCode))"
Assert-That (Test-Path $appExe) 'QuietFan.exe is installed per user'
Assert-That (Test-UninstallEntry) 'an uninstall entry is registered'
Assert-That (Test-RunValue) 'the autostart task creates the Run value'

$uninstaller = Join-Path (Split-Path $appExe) 'unins000.exe'
Start-Process $uninstaller -ArgumentList $silent -Wait
# The uninstaller re-launches itself from %TEMP%; wait until it has finished removing everything.
$deadline = (Get-Date).AddSeconds(60)
while (((Test-Path $appExe) -or (Test-UninstallEntry)) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 500 }
Assert-That (-not (Test-Path $appExe)) 'QuietFan.exe is removed'
Assert-That (-not (Test-UninstallEntry)) 'the uninstall entry is removed'
Assert-That (-not (Test-RunValue)) 'the Run value is removed'
