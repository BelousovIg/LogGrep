<#
.SYNOPSIS
    Builds the release archives.

.DESCRIPTION
    Runs the tests, then publishes two builds of the app and zips each one:

      LogGrep-<version>-win-x64.zip             needs the .NET 8 desktop runtime, a few hundred KB
      LogGrep-<version>-win-x64-standalone.zip  carries the runtime, runs on a machine without .NET

    The version is whatever <Version> says in src/LogGrep/LogGrep.csproj, and the script only reads
    it - to release a new one, change it there and commit it. A build that could be told a version
    on the command line can produce two different archives from the same commit, and then the number
    on the box no longer identifies what is inside it.

.EXAMPLE
    .\publish.ps1
#>
[CmdletBinding()]
param(
    # Where the archives end up.
    [string] $Output = 'artifacts',

    # Publish without checking the tests first. For a throwaway build only.
    [switch] $SkipTests
)

$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot

try {
    $project = 'src/LogGrep/LogGrep.csproj'
    $staging = Join-Path $Output 'staging'

    $Version = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ }
    if (-not $Version) { throw "No <Version> in $project. The release version lives there." }
    Write-Host "Version $Version, from $project" -ForegroundColor Cyan

    if (-not $SkipTests) {
        Write-Host 'Testing...' -ForegroundColor Cyan
        dotnet test --configuration Release --nologo
        if ($LASTEXITCODE -ne 0) { throw 'The tests failed. Nothing was published.' }
    }

    if (Test-Path $Output) { Remove-Item $Output -Recurse -Force }
    New-Item -ItemType Directory -Path $staging -Force | Out-Null

    Write-Host 'Publishing against an installed runtime...' -ForegroundColor Cyan
    dotnet publish $project --configuration Release --runtime win-x64 --self-contained false `
        -p:DebugType=none --output "$staging/runtime" --nologo
    if ($LASTEXITCODE -ne 0) { throw 'The runtime-dependent build failed.' }

    Write-Host 'Publishing with the runtime bundled in...' -ForegroundColor Cyan
    dotnet publish $project --configuration Release --runtime win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=none --output "$staging/standalone" --nologo
    if ($LASTEXITCODE -ne 0) { throw 'The standalone build failed.' }

    $plain = Join-Path $Output "LogGrep-$Version-win-x64.zip"
    $standalone = Join-Path $Output "LogGrep-$Version-win-x64-standalone.zip"

    Write-Host 'Packing...' -ForegroundColor Cyan
    Compress-Archive -Path "$staging/runtime/*" -DestinationPath $plain
    Compress-Archive -Path "$staging/standalone/*" -DestinationPath $standalone
    Remove-Item $staging -Recurse -Force

    Write-Host ''
    Write-Host 'Done. Upload these to a GitHub release:' -ForegroundColor Green
    Get-ChildItem $Output -Filter *.zip |
        Select-Object Name, @{ Name = 'Size'; Expression = { '{0:N1} MB' -f ($_.Length / 1MB) } } |
        Format-Table -AutoSize
}
finally {
    Pop-Location
}
