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

# Inno Setup records its folder and version in its uninstall entry; ISCC.exe itself carries no version number.
function Find-InnoSetup {
    $keys = 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1',
            'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1',
            'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1'
    foreach ($key in $keys) {
        $entry = Get-ItemProperty -Path $key -ErrorAction SilentlyContinue
        if ($null -eq $entry) { continue }
        $compiler = Join-Path $entry.InstallLocation 'ISCC.exe'
        if (Test-Path $compiler) { return [pscustomobject]@{ Compiler = $compiler; Version = [version]$entry.DisplayVersion } }
    }
    return $null
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
    $inno = Find-InnoSetup
    if ($null -eq $inno) { throw 'Inno Setup 6 not found: install it from https://jrsoftware.org/isinfo.php' }
    Write-Host "Using $($inno.Compiler) (Inno Setup $($inno.Version))"

    $defines = "/DAppVersion=$Version", "/DAppExe=$exe", "/DRepoDir=$root", "/DOutputDir=$dist", "/DIconFile=$icon"
    $chineseIsl = Join-Path (Split-Path $inno.Compiler) 'Languages\ChineseSimplified.isl'
    if (-not (Test-Path $chineseIsl)) {
        # Inno Setup 6 keeps Simplified Chinese among its unofficial translations, which it does not install: fetch
        # the file for this version and save it with a BOM, the only way ISCC reads it as UTF-8.
        $tag = 'is-{0}_{1}_{2}' -f $inno.Version.Major, $inno.Version.Minor, $inno.Version.Build
        $url = "https://raw.githubusercontent.com/jrsoftware/issrc/$tag/Files/Languages/Unofficial/ChineseSimplified.isl"
        $chineseIsl = Join-Path $obj 'ChineseSimplified.isl'
        try {
            $translation = (Invoke-WebRequest -UseBasicParsing -Uri $url).Content
            [IO.File]::WriteAllText($chineseIsl, $translation, [Text.UTF8Encoding]::new($true))
        }
        catch {
            Write-Warning "Simplified Chinese translation unavailable from $url : $($_.Exception.Message)"
            $chineseIsl = $null
        }
    }
    if ($chineseIsl) {
        $defines += "/DChineseIsl=$chineseIsl"
        Write-Host "Including the Simplified Chinese installer translation: $chineseIsl"
    }
    else {
        Write-Warning 'Building an English-only installer.'
    }

    # ISCC reads a script as UTF-8 only when it starts with a byte order mark.
    $script = Join-Path $obj 'QuietFan.iss'
    $text = [IO.File]::ReadAllText((Join-Path $root 'installer\QuietFan.iss'))
    [IO.File]::WriteAllText($script, $text, [Text.UTF8Encoding]::new($true))

    & $inno.Compiler /Q @defines $script
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
