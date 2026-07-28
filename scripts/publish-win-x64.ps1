param(
    [string]$OutputDirectory = "$PSScriptRoot\..\artifacts\win-x64"
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "..\src\WindowsShareManager\WindowsShareManager.csproj"

dotnet publish $project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    --output $OutputDirectory

Write-Host "배포본 생성 완료: $OutputDirectory"
