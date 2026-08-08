# One-time setup: registers two Windows Scheduled Tasks under the current user
# (runs only while you're logged in - that's sufficient as long as this PC stays
# logged in, which Docker Desktop's own "start at login" setting already assumes).
#   - CarShowJudging-PollDeploy every 15 min -> picks up new images pushed by CI
#   - CarShowJudging-Backup    daily at 3am  -> tars DB + uploads to .\backups
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

function Register-RepeatingTask {
    param($Name, $ScriptPath, $IntervalMinutes)
    $action = New-ScheduledTaskAction -Execute "powershell.exe" `
        -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$ScriptPath`""
    $trigger = New-ScheduledTaskTrigger -Once -At (Get-Date) `
        -RepetitionInterval (New-TimeSpan -Minutes $IntervalMinutes) `
        -RepetitionDuration (New-TimeSpan -Days 3650)
    Register-ScheduledTask -TaskName $Name -Action $action -Trigger $trigger -Force -ErrorAction Stop | Out-Null
    Write-Host "Registered $Name (every $IntervalMinutes min)"
}

Register-RepeatingTask "CarShowJudging-PollDeploy" (Join-Path $repoRoot "scripts\poll-deploy.ps1") 15

$backupAction = New-ScheduledTaskAction -Execute "powershell.exe" `
    -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$repoRoot\scripts\backup.ps1`""
$backupTrigger = New-ScheduledTaskTrigger -Daily -At "3:00AM"
Register-ScheduledTask -TaskName "CarShowJudging-Backup" -Action $backupAction -Trigger $backupTrigger -Force | Out-Null
Write-Host "Registered CarShowJudging-Backup (daily 3am)"

Write-Host "`nDone. View/manage these under Task Scheduler -> Task Scheduler Library."
Write-Host "Also check Docker Desktop Settings -> General -> 'Start Docker Desktop when you log in' is ON,"
Write-Host "so containers (restart: unless-stopped) come back automatically after a reboot."
