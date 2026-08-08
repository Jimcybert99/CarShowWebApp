# Tars the app's SQLite DB + uploaded photos (both live in Docker named volumes)
# into a dated archive under .\backups, and prunes anything older than 14 days.
# Intended to run daily on a Windows Scheduled Task.
$repoRoot = Join-Path $PSScriptRoot ".."
Set-Location $repoRoot

function Get-ComposeVolume([string]$volumeLabel) {
    $name = docker volume ls --filter "label=com.docker.compose.volume=$volumeLabel" --format "{{.Name}}" | Select-Object -First 1
    if (-not $name) { throw "Could not find a Docker volume labeled '$volumeLabel' - has 'docker compose up -d' been run yet?" }
    return $name
}

$dataVolume = Get-ComposeVolume "carshow-data"
$uploadsVolume = Get-ComposeVolume "carshow-uploads"

$backupDir = Join-Path $repoRoot "backups"
New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
$stamp = Get-Date -Format "yyyy-MM-dd"

docker run --rm `
    -v "${dataVolume}:/data:ro" `
    -v "${uploadsVolume}:/uploads:ro" `
    -v "${backupDir}:/backup" `
    alpine tar czf "/backup/carshow-$stamp.tar.gz" -C / data uploads

Get-ChildItem $backupDir -Filter "*.tar.gz" |
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-14) } |
    Remove-Item -Force

Write-Host "$(Get-Date -Format o) Backup written: carshow-$stamp.tar.gz"
