<#
.SYNOPSIS
    Builds QuietFan with the C# compiler that ships with Windows (.NET Framework 4.x); no SDK needed.
.EXAMPLE
    ./build.ps1                  # dist\QuietFan.exe
#>
[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.0'
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
