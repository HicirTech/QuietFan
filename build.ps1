<#
.SYNOPSIS
    Builds QuietFan with the C# compiler that ships with Windows (.NET Framework 4.x); no SDK needed.
.EXAMPLE
    ./build.ps1                  # dist\QuietFan.exe
    ./build.ps1 -Test            # also build and run the unit tests
#>
[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.0',
    [switch]$Test
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = $PSScriptRoot
$dist = Join-Path $root 'dist'
$obj = Join-Path $root 'obj'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$references = '-r:System.Windows.Forms.dll', '-r:System.Drawing.dll'

function Invoke-Csc([string[]]$Arguments) {
    & $csc -nologo -codepage:65001 -warnaserror+ @Arguments
    if ($LASTEXITCODE -ne 0) { throw "csc failed with exit code $LASTEXITCODE" }
}

New-Item -ItemType Directory -Force -Path $dist, $obj | Out-Null

$assemblyInfo = Join-Path $obj 'AssemblyInfo.cs'
Set-Content -Path $assemblyInfo -Encoding utf8 -Value @"
[assembly: System.Reflection.AssemblyTitle("QuietFan")]
[assembly: System.Reflection.AssemblyProduct("QuietFan")]
[assembly: System.Reflection.AssemblyCopyright("Copyright (c) 2026 HicirTech")]
[assembly: System.Reflection.AssemblyVersion("$Version.0")]
[assembly: System.Reflection.AssemblyFileVersion("$Version.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("$Version")]
"@

$icon = Join-Path $obj 'QuietFan.ico'
& (Join-Path $root 'tools\New-AppIcon.ps1') -Path $icon

$appSources = @(Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object FullName)
$exe = Join-Path $dist 'QuietFan.exe'
Invoke-Csc (@('-optimize+', '-target:winexe', "-win32icon:$icon", "-out:$exe") + $references + $appSources + $assemblyInfo)
Write-Host "Built $exe ($Version)"

if ($Test) {
    # The tests link every app source except the UI entry points, so they run headless.
    $librarySources = $appSources | Where-Object { (Split-Path $_ -Leaf) -notin 'Program.cs', 'TrayApp.cs' }
    $testSources = @(Get-ChildItem (Join-Path $root 'tests') -Filter *.cs | ForEach-Object FullName)
    $testExe = Join-Path $obj 'QuietFan.Tests.exe'
    Invoke-Csc (@('-target:exe', "-out:$testExe") + $references + $librarySources + $testSources)
    & $testExe
    if ($LASTEXITCODE -ne 0) { throw "$LASTEXITCODE unit test(s) failed" }
}
