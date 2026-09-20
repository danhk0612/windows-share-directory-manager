param(
    [string]$OutputDirectory = "$PSScriptRoot\..\artifacts\win-x64"
)

$ErrorActionPreference = "Stop"

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$appProject = Join-Path $repositoryRoot "src\WindowsShareManager\WindowsShareManager.csproj"
$launcherProject = Join-Path $repositoryRoot "src\WindowsShareManager.Launcher\WindowsShareManager.Launcher.csproj"
$appPublishDirectory = Join-Path $repositoryRoot "artifacts\publish-app"
$launcherPublishDirectory = Join-Path $repositoryRoot "artifacts\publish-launcher"

[xml]$project = Get-Content $appProject
$version = [string]$project.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "프로젝트 버전을 읽지 못했습니다."
}

foreach ($directory in @($OutputDirectory, $appPublishDirectory, $launcherPublishDirectory)) {
    if (Test-Path $directory) {
        Remove-Item $directory -Recurse -Force
    }
    New-Item -ItemType Directory -Path $directory | Out-Null
}

dotnet publish $appProject `
    --configuration Release `
    --runtime win-x64 `
    --self-contained false `
    -p:PublishSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    --output $appPublishDirectory

dotnet publish $launcherProject `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:Version=$version `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -p:StripSymbols=true `
    --output $launcherPublishDirectory

$appExecutable = Join-Path $appPublishDirectory "WindowsShareManager.App.exe"
$launcherExecutable = Join-Path $launcherPublishDirectory "WindowsShareManager.exe"

if (-not (Test-Path $appExecutable)) {
    throw "실제 앱 실행 파일을 찾지 못했습니다: $appExecutable"
}
if (-not (Test-Path $launcherExecutable)) {
    throw "런처 실행 파일을 찾지 못했습니다: $launcherExecutable"
}

Copy-Item $launcherExecutable (Join-Path $OutputDirectory "WindowsShareManager.exe")
Copy-Item $appExecutable (Join-Path $OutputDirectory "WindowsShareManager.App.exe")

Remove-Item $appPublishDirectory -Recurse -Force
Remove-Item $launcherPublishDirectory -Recurse -Force

$files = @(Get-ChildItem $OutputDirectory -File)
if ($files.Count -ne 2) {
    throw "배포 파일은 2개여야 합니다. 실제 파일 수: $($files.Count)"
}

Write-Host "배포본 생성 완료: $OutputDirectory"
$files | Select-Object Name, Length | Format-Table -AutoSize
