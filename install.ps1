$ErrorActionPreference = "Stop"

$repo = "SimplicatedGamesStudio/unity-cli"
$installDir = "$env:LOCALAPPDATA\unity-cli"
$exe = "$installDir\unity-cli.exe"

New-Item -ItemType Directory -Force -Path $installDir | Out-Null

$url = "https://github.com/$repo/releases/latest/download/unity-cli-windows-amd64.exe"
Write-Host "Downloading unity-cli for windows/amd64..."
try {
    Invoke-WebRequest -Uri $url -OutFile $exe -UseBasicParsing
} catch {
    Write-Host "Release binary not available for $repo. Falling back to 'go install'."

    $go = Get-Command go -ErrorAction SilentlyContinue
    if (-not $go) {
        throw "Go is required for the fallback install path. Install Go or publish a release asset first."
    }

    $env:GOBIN = $installDir
    & $go.Source install "github.com/$repo@latest"
    if ($LASTEXITCODE -ne 0) {
        throw "go install failed with exit code $LASTEXITCODE"
    }
}

$userPath = [Environment]::GetEnvironmentVariable("Path", "User")
if ($userPath -notlike "*$installDir*") {
    [Environment]::SetEnvironmentVariable("Path", "$installDir;$userPath", "User")
    $env:Path = "$installDir;$env:Path"
    Write-Host "Added $installDir to PATH (restart shell to apply)"
}

Write-Host "Installed unity-cli to $exe"
& $exe version
