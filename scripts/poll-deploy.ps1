# Pulls the latest image pushed to GHCR by .github/workflows/build.yml and restarts
# the app if it changed. Intended to run on a repeating Windows Scheduled Task -
# there's no SSH server exposed on this home network for GitHub Actions to push to,
# so this machine polls instead.
. (Join-Path $PSScriptRoot "_env.ps1")
$repoRoot = Join-Path $PSScriptRoot ".."
Set-Location $repoRoot
Import-DotEnv

$before = docker compose images -q carshowjudging
docker compose pull
$after = docker compose images -q carshowjudging

if ($before -ne $after) {
    Write-Host "$(Get-Date -Format o) New image detected, restarting..."
    docker compose up -d
    docker image prune -f
} else {
    Write-Host "$(Get-Date -Format o) No change."
}
