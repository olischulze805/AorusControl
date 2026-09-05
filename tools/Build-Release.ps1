<#
.SYNOPSIS
    Builds AORUS Control and packs it into a single Setup.exe with update support.

.DESCRIPTION
    Publishes the app and the hardware worker into one folder, then hands that folder to
    Velopack's `vpk`, which produces:

        Setup.exe            the installer the user runs (per-user, no admin prompt)
        AorusControl-<v>-full.nupkg   the release package the app updates itself from
        RELEASES / releases.<channel>.json

    Everything the app needs at runtime is inside that folder - the worker included, since
    Fixed mode is not safe without it - so there is nothing else to install afterwards.
    .NET itself is bundled (self-contained), because "install this app" should not turn
    into "first install a runtime".

    Publish to a GitHub release by uploading the whole Releases folder; the app's update
    check reads that same release feed.

.PARAMETER Version
    The release version, e.g. 0.2.0. Must be higher than the installed one for the update
    check to offer it. Defaults to the App project's own <Version>.

.EXAMPLE
    pwsh tools/Build-Release.ps1 -Version 0.2.0
#>
[CmdletBinding()]
param(
    [string] $Version,
    [string] $Channel = "win",
    [switch] $SkipTests
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (-not $Version) {
    $projectXml = [xml](Get-Content "src/AorusControl.App/AorusControl.App.csproj")
    $Version = $projectXml.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
}
if (-not $Version) { throw "Keine Version gefunden; bitte -Version angeben." }

$staging = "artifacts/publish"
$releases = "artifacts/releases"

Write-Host "== AORUS Control $Version ==" -ForegroundColor Cyan

if (-not $SkipTests) {
    Write-Host "-- Tests" -ForegroundColor Cyan
    dotnet run --project tests/AorusControl.App.SmokeTests/AorusControl.App.SmokeTests.csproj -c Release -v:q
    if ($LASTEXITCODE -ne 0) { throw "Smoke-Tests fehlgeschlagen; es wird nichts gepackt." }
}

Write-Host "-- Publish" -ForegroundColor Cyan
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
# Both executables land in the same folder: WorkerLauncher looks for the worker next to the
# app first, which is exactly the installed layout.
dotnet publish src/AorusControl.App/AorusControl.App.csproj -c Release -r win-x64 --self-contained true `
    -p:Version=$Version -p:PublishSingleFile=false -o $staging
if ($LASTEXITCODE -ne 0) { throw "Publish der App fehlgeschlagen." }
dotnet publish src/AorusControl.Worker/AorusControl.Worker.csproj -c Release -r win-x64 --self-contained true `
    -p:Version=$Version -o $staging
if ($LASTEXITCODE -ne 0) { throw "Publish des Workers fehlgeschlagen." }

Write-Host "-- Pack" -ForegroundColor Cyan
dotnet tool restore
if ($LASTEXITCODE -ne 0) { throw "vpk konnte nicht wiederhergestellt werden." }
dotnet vpk pack `
    --packId AorusControl `
    --packVersion $Version `
    --packDir $staging `
    --mainExe AorusControl.exe `
    --packTitle "AORUS Control" `
    --packAuthors "olischulze805" `
    --icon src/AorusControl.App/Assets/app.ico `
    --channel $Channel `
    --outputDir $releases
if ($LASTEXITCODE -ne 0) { throw "vpk pack fehlgeschlagen." }

Write-Host ""
Write-Host "Fertig. Setup und Update-Paket liegen in ${releases}:" -ForegroundColor Green
Get-ChildItem $releases | Select-Object Name, @{ Name = "MB"; Expression = { [math]::Round($_.Length / 1MB, 1) } } | Format-Table
