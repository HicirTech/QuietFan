<#
.SYNOPSIS
    Builds QuietFan with the C# compiler that ships with Windows (.NET Framework 4.x); no SDK needed.
.EXAMPLE
    ./build.ps1                  # dist\QuietFan.exe
    ./build.ps1 -Test            # also build and run the unit tests
    ./build.ps1 -Installer       # also build dist\QuietFan-Setup-<version>.exe (needs Inno Setup 6)
    ./build.ps1 -Package         # also build the portable zip, SHA256SUMS.txt and release-notes.md
#>
[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.0',
    [switch]$Test,
    [switch]$Installer,
    [switch]$Package
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

if ($Installer) {
    $iscc = @(
        (Get-Command ISCC.exe -ErrorAction SilentlyContinue | ForEach-Object Source),
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    ) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
    if (-not $iscc) { throw 'ISCC.exe not found: install Inno Setup 6 from https://jrsoftware.org/isinfo.php' }

    $defines = "/DAppVersion=$Version", "/DAppExe=$exe", "/DRepoDir=$root", "/DOutputDir=$dist", "/DIconFile=$icon"
    # Simplified Chinese ships with Inno Setup 6.5 and later; older versions build an English-only installer.
    $chineseIsl = Join-Path (Split-Path $iscc) 'Languages\ChineseSimplified.isl'
    if (Test-Path $chineseIsl) { $defines += "/DChineseIsl=$chineseIsl" }
    else { Write-Warning 'This Inno Setup has no Simplified Chinese translation; building an English-only installer.' }

    # ISCC reads a script as UTF-8 only when it starts with a byte order mark.
    $script = Join-Path $obj 'QuietFan.iss'
    $text = [IO.File]::ReadAllText((Join-Path $root 'installer\QuietFan.iss'))
    [IO.File]::WriteAllText($script, $text, [Text.UTF8Encoding]::new($true))

    & $iscc /Q @defines $script
    if ($LASTEXITCODE -ne 0) { throw "ISCC failed with exit code $LASTEXITCODE" }
    Write-Host "Built $(Join-Path $dist "QuietFan-Setup-$Version.exe")"
}

if ($Package) {
    $portable = Join-Path $obj 'portable'
    if (Test-Path $portable) { Remove-Item $portable -Recurse -Force }
    New-Item -ItemType Directory -Path $portable | Out-Null
    Copy-Item -Path $exe, (Join-Path $root 'README.md'), (Join-Path $root 'LICENSE') -Destination $portable
    $zip = Join-Path $dist "QuietFan-$Version-portable.zip"
    Compress-Archive -Path (Join-Path $portable '*') -DestinationPath $zip -Force

    $assets = @(Get-ChildItem $dist -File | Where-Object { $_.Name -in "QuietFan-Setup-$Version.exe", "QuietFan-$Version-portable.zip" })
    $sums = $assets | ForEach-Object { '{0}  {1}' -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name }
    Set-Content -Path (Join-Path $dist 'SHA256SUMS.txt') -Value $sums -Encoding ascii

    # The release notes are this version's CHANGELOG section plus install hints.
    $changelog = [IO.File]::ReadAllText((Join-Path $root 'CHANGELOG.md'))
    $section = [regex]::Match($changelog, '(?ms)^## \[' + [regex]::Escape($Version) + '\][^\r\n]*\r?\n(.*?)(?=^## \[|\z)')
    if (-not $section.Success) { throw "CHANGELOG.md has no section for $Version" }
    $notes = @(
        $section.Groups[1].Value.Trim()
        ''
        '### Install'
        ''
        "Download ``QuietFan-Setup-$Version.exe`` and run it: it installs for the current user and needs no administrator rights."
        'The installer is not code-signed, so Windows SmartScreen may ask for confirmation (More info → Run anyway).'
        "``QuietFan-$Version-portable.zip`` runs without installation. SHA-256 checksums are in ``SHA256SUMS.txt``."
    )
    Set-Content -Path (Join-Path $dist 'release-notes.md') -Value $notes -Encoding utf8
    Write-Host "Packaged $zip, SHA256SUMS.txt and release-notes.md"
}
