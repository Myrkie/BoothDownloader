$ErrorActionPreference = "Stop"

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker is not installed or could not be found in PATH. Please install Docker Desktop."
}

docker info 2>&1 | Out-Null

if ($LASTEXITCODE -ne 0) {
    throw @"
Docker is unavailable. Make sure Docker Desktop is installed and running.

You can start Docker Desktop and try again.
"@
}

$ProjectDir = Join-Path $PSScriptRoot "BoothDownloader"
$PublishDir = Join-Path $PSScriptRoot "publish\Linux64"

Write-Host "Building Linux Native AOT Docker image..."

docker build -f (Join-Path $ProjectDir "Dockerfile") -t booth-downloader-publisher $PSScriptRoot

if ($LASTEXITCODE -ne 0) {
    throw "Docker build failed with exit code $LASTEXITCODE. Check the Docker build output above."
}

New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null

$PublishDir = (Resolve-Path $PublishDir).Path
$DockerPublishDir = $PublishDir -replace '\\', '/'

Write-Host "Exporting Linux Native AOT publish..."

docker run --rm `
    --mount "type=bind,source=$DockerPublishDir,target=/export" `
    --entrypoint /bin/bash `
    booth-downloader-publisher `
    -c 'cp -a /out/. /export/'

if ($LASTEXITCODE -ne 0) {
    throw "Docker export failed with exit code $LASTEXITCODE."
}

Write-Host ""
Write-Host "Linux Native AOT publish completed: $PublishDir" -ForegroundColor Green