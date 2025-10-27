# Build script for UnityUniversalVr solution
param (
    [string]$Configuration = "Release",
    [string]$Platform = "Any CPU",
    [string]$MSBuildToolsPath = "C:\Program Files (x86)\JetBrains\JetBrains Rider 2025.1\tools\MSBuild\Current\Bin\amd64\MSBuild.exe"
)

if (-not $MSBuildToolsPath) {
    Write-Error "MSBuild not found!"
    exit 1
}

# Build configurations
$configurations = @(
    "legacy-mono",
    "modern-mono",
    "legacy-il2cpp"
#    "modern-il2cpp"
)

Write-Host "Building all configurations..." -ForegroundColor Green

foreach ($config in $configurations) {
    Write-Host "Building configuration: $config" -ForegroundColor Cyan

    # Optionally restore solution before building it
    & $MSBuildToolsPath "UnityUniversalVr.sln" /t:Restore /p:Configuration=$config

    # Build Uuvr project
    & $MSBuildToolsPath "UnityUniversalVr.sln" `
        /p:Configuration=$config `
        /v:minimal

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed for configuration: $config"
        exit 1
    }
}

Write-Host "All builds completed successfully!" -ForegroundColor Green
