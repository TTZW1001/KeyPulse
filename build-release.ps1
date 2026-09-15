[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$IsccPath = ""
)

$ErrorActionPreference = "Stop"
$repositoryRoot = $PSScriptRoot
$publishDirectory = Join-Path $repositoryRoot "publish\$Runtime"
$releaseDirectory = Join-Path $repositoryRoot "release"
$portablePath = Join-Path $releaseDirectory "KeyPulse-portable.zip"
$checksumsPath = Join-Path $releaseDirectory "checksums.txt"

foreach ($path in @($publishDirectory, $releaseDirectory)) {
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}

New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null

dotnet publish (Join-Path $repositoryRoot "src\KeyPulse.App\KeyPulse.App.csproj") `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    -m:1 `
    --output $publishDirectory `
    -p:DebugType=None `
    -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Compress-Archive -Path (Join-Path $publishDirectory "*") -DestinationPath $portablePath

if ([string]::IsNullOrWhiteSpace($IsccPath)) {
    $candidates = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe",
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
    )
    $IsccPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

if (-not [string]::IsNullOrWhiteSpace($IsccPath)) {
    & $IsccPath "/Qp" (Join-Path $repositoryRoot "installer\KeyPulse.iss")
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup failed with exit code $LASTEXITCODE"
    }
}
else {
    Write-Warning "Inno Setup 6 was not found; portable artifact was created without the installer."
}

$artifacts = Get-ChildItem -LiteralPath $releaseDirectory -File |
    Where-Object { $_.Name -ne "checksums.txt" } |
    Sort-Object Name
$checksumLines = foreach ($artifact in $artifacts) {
    $hash = (Get-FileHash -LiteralPath $artifact.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($artifact.Name)"
}
Set-Content -LiteralPath $checksumsPath -Value $checksumLines -Encoding ascii

Write-Output "Release artifacts:"
Get-ChildItem -LiteralPath $releaseDirectory -File | Select-Object Name, Length
