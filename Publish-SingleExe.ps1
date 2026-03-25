param(
    [string]$Configuration = "Release",
    [string]$Output = "artifacts\\singlefile"
)

$ErrorActionPreference = "Stop"

dotnet publish NNotify.csproj -c $Configuration -p:SelfContained=false -p:PublishSelfContained=false -o $Output

$exePath = Join-Path $Output "NNotify.exe"
if (-not (Test-Path $exePath)) {
    throw "Publish completed, but NNotify.exe was not found in '$Output'."
}

$sizeMb = [math]::Round((Get-Item $exePath).Length / 1MB, 2)
Write-Host "Single-file publish completed: $exePath ($sizeMb MB)"
