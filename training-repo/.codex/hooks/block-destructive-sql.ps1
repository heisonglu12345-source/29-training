$inputJson = [Console]::In.ReadToEnd()

if ($inputJson -match 'DROP\s+TABLE|TRUNCATE(?:\s+TABLE)?') {
    [Console]::Error.WriteLine('Action denied: destructive SQL is not allowed.')
    exit 2
}

exit 0
