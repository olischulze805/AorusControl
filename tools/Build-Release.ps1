<#
.SYNOPSIS
    Validates, publishes and packages one reproducible AORUS Control release.

.DESCRIPTION
    The project version is the single source of truth. Unless -SkipChecks is explicitly used,
    the script builds the solution and runs both the logic suite and the offscreen UI suite
    before creating any package. Velopack history already present in artifacts/releases is
    retained so a previous full package can be used to produce a small delta package.

    The script creates versioned release assets and SHA256SUMS.txt. Publishing to GitHub is
    deliberately owned by .github/workflows/release.yml, where the tag and release notes are
    checked before this script runs.
#>
[CmdletBinding()]
param(
    [string] $Version,
    [ValidatePattern('^[a-z0-9][a-z0-9.-]*$')]
    [string] $Channel = "win",
    [Alias("SkipTests")]
    [switch] $SkipChecks
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$projectPath = "src/AorusControl.App/AorusControl.App.csproj"
$projectXml = [xml](Get-Content $projectPath)
$projectVersion = $projectXml.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $projectVersion) { throw "In $projectPath wurde keine <Version> gefunden." }
if (-not $Version) { $Version = $projectVersion }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version '$Version' ist ungültig; erwartet wird MAJOR.MINOR.PATCH." }
if ($Version -ne $projectVersion) {
    throw "Release-Version $Version stimmt nicht mit <$projectPath> ($projectVersion) überein."
}

$staging = "artifacts/publish"
$releases = "artifacts/releases"
$setupProduced = Join-Path $releases "AorusControl-$Channel-Setup.exe"
$setupVersioned = Join-Path $releases "AorusControl-$Version-Setup.exe"
$fullPackage = Join-Path $releases "AorusControl-$Version-full.nupkg"
$deltaPackage = Join-Path $releases "AorusControl-$Version-delta.nupkg"
$feed = Join-Path $releases "releases.$Channel.json"
$checksums = Join-Path $releases "SHA256SUMS.txt"

Write-Host "== AORUS Control $Version ==" -ForegroundColor Cyan

if (-not $SkipChecks) {
    Write-Host "-- Release build" -ForegroundColor Cyan
    dotnet build AorusControl.slnx --configuration Release
    if ($LASTEXITCODE -ne 0) { throw "Release-Build fehlgeschlagen; es wird nichts gepackt." }

    Write-Host "-- Logic checks" -ForegroundColor Cyan
    dotnet run --project tests/AorusControl.App.SmokeTests --configuration Release
    if ($LASTEXITCODE -ne 0) { throw "Smoke-Tests fehlgeschlagen; es wird nichts gepackt." }

    Write-Host "-- Offscreen UI checks" -ForegroundColor Cyan
    dotnet run --project tests/AorusControl.UiChecks --configuration Release -- --verify-only
    if ($LASTEXITCODE -ne 0) { throw "UI-Checks fehlgeschlagen; es wird nichts gepackt." }
}

Write-Host "-- Publish" -ForegroundColor Cyan
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Force -Path $releases | Out-Null

dotnet publish $projectPath --configuration Release --runtime win-x64 --self-contained true `
    -p:Version=$Version -p:PublishSingleFile=false --output $staging
if ($LASTEXITCODE -ne 0) { throw "Publish der App fehlgeschlagen." }
dotnet publish src/AorusControl.Worker/AorusControl.Worker.csproj --configuration Release --runtime win-x64 `
    --self-contained true -p:Version=$Version --output $staging
if ($LASTEXITCODE -ne 0) { throw "Publish des Workers fehlgeschlagen." }

Write-Host "-- Velopack" -ForegroundColor Cyan
# A repeat build of the same version must never use its own stale package as the delta base.
# Keep older releases, but remove this version from both disk and the generated feed first.
foreach ($currentOutput in @($setupProduced, $setupVersioned, $fullPackage, $deltaPackage, $checksums)) {
    if (Test-Path $currentOutput) { Remove-Item -LiteralPath $currentOutput -Force }
}
if (Test-Path $feed) {
    $feedIndex = Get-Content -LiteralPath $feed -Raw | ConvertFrom-Json
    $feedIndex.Assets = @($feedIndex.Assets | Where-Object { $_.Version -ne $Version })
    $feedIndex | ConvertTo-Json -Depth 10 -Compress | Set-Content -LiteralPath $feed -Encoding utf8NoBOM
}

dotnet tool restore
if ($LASTEXITCODE -ne 0) { throw "Velopack konnte nicht wiederhergestellt werden." }
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
if ($LASTEXITCODE -ne 0) { throw "Velopack-Paketierung fehlgeschlagen." }

foreach ($required in @($setupProduced, $fullPackage, $feed)) {
    if (-not (Test-Path $required)) { throw "Erwartetes Release-Artefakt fehlt: $required" }
}
Copy-Item -LiteralPath $setupProduced -Destination $setupVersioned -Force

$releaseAssets = @($setupVersioned, $fullPackage, $feed)
if (Test-Path $deltaPackage) { $releaseAssets += $deltaPackage }
$checksumLines = foreach ($asset in $releaseAssets) {
    $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $asset
    "{0}  {1}" -f $hash.Hash.ToLowerInvariant(), (Split-Path -Leaf $asset)
}
Set-Content -LiteralPath $checksums -Value $checksumLines -Encoding utf8NoBOM
$releaseAssets += $checksums

Write-Host ""
Write-Host "Release-Artefakte:" -ForegroundColor Green
Get-Item $releaseAssets | Select-Object Name, @{ Name = "MB"; Expression = { [math]::Round($_.Length / 1MB, 1) } } | Format-Table
