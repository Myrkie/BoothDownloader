$DockerCommand = Get-Command docker -ErrorAction SilentlyContinue

if (-not $DockerCommand) {
    throw "Docker is not installed or could not be found in PATH. Please install Docker Desktop."
}

Write-Host "Checking Docker availability..."

$DockerOutput = & docker info 2>&1
$DockerExitCode = $LASTEXITCODE

if ($DockerExitCode -ne 0) {
    Write-Host ""
    Write-Host "Docker is installed, but the Docker daemon is unavailable." -ForegroundColor Red
    Write-Host "Make sure Docker Desktop is running and using Linux containers."
    Write-Host ""

    if ($DockerOutput) {
        Write-Host ($DockerOutput | Out-String)
    }

    throw "Docker is unavailable. Start Docker Desktop and try again."
}

$ProjectDir = Join-Path $PSScriptRoot "BoothDownloader"
$PublishDir = Join-Path $PSScriptRoot "publish\Linux64"

New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null

$PublishDir = (Resolve-Path $PublishDir).Path
$DockerPublishDir = $PublishDir -replace '\\', '/'

Write-Host "Publishing Linux Native AOT directly to output directory..."

docker build -f (Join-Path $ProjectDir "Dockerfile") --output "type=local,dest=$DockerPublishDir" $PSScriptRoot

if ($LASTEXITCODE -ne 0) {
    throw "Docker publish failed with exit code $LASTEXITCODE. Check the Docker build output above."
}

Write-Host ""
Write-Host "Linux Native AOT publish completed: $PublishDir" -ForegroundColor Green