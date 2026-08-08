function Import-DotEnv {
    param([string]$Path = (Join-Path $PSScriptRoot "..\.env"))
    if (-not (Test-Path $Path)) {
        throw ".env not found at $Path - copy .env.example to .env and fill it in first."
    }
    Get-Content $Path | ForEach-Object {
        $line = $_.Trim()
        if ($line -and -not $line.StartsWith("#") -and $line.Contains("=")) {
            $key, $value = $line.Split("=", 2)
            [Environment]::SetEnvironmentVariable($key.Trim(), $value.Trim(), "Process")
        }
    }
}
