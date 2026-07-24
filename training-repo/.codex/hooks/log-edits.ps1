$payload = [Console]::In.ReadToEnd() | ConvertFrom-Json

$file = $payload.tool_input.file_path
if (-not $file) {
    $file = $payload.tool_response.filePath
}
if (-not $file) {
    exit 0
}

$logPath = Join-Path $PSScriptRoot 'edit-log.txt'
$line = '{0}  {1}  {2}' -f (
    Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
), $payload.tool_name, $file
Add-Content -Path $logPath -Value $line

@{
    systemMessage = "PostToolUse hook fired: $($payload.tool_name) -> $file (logged to .codex/hooks/edit-log.txt)"
} | ConvertTo-Json -Compress
