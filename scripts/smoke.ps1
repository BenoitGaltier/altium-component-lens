param([string]$Executable = '')
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (!$Executable) { $Executable = Join-Path $repo 'dist/win-x64/ComponentLens.exe' }
New-Item -ItemType Directory -Force -Path (Join-Path $repo '.artifacts') | Out-Null
$runId = [guid]::NewGuid().ToString('N')
$stdoutPath = Join-Path $repo ".artifacts/smoke-$runId.out"
$stderrPath = Join-Path $repo ".artifacts/smoke-$runId.err"
$previousRoot = $env:DOTNET_ROOT
$previousLookup = $env:DOTNET_MULTILEVEL_LOOKUP
$script:assertions = 0
function Assert-Lens($Condition, [string]$Message) {
    if (!$Condition) { throw $Message }
    $script:assertions++
}
try {
    # The published executable must not resolve a separately installed .NET runtime.
    $env:DOTNET_ROOT = Join-Path $repo '.artifacts/nonexistent-runtime'
    $env:DOTNET_MULTILEVEL_LOOKUP = '0'
    $process = Start-Process -FilePath $Executable -ArgumentList '--headless','--port','0','--data-dir',".artifacts/smoke-$runId-state" -WorkingDirectory $repo -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
    $address = $null
    for ($attempt = 0; $attempt -lt 100; $attempt++) {
        if ($process.HasExited) { throw 'Portable process exited before startup' }
        $output = Get-Content -LiteralPath $stdoutPath -Raw -ErrorAction SilentlyContinue
        if ($output -match '(http://127\.0\.0\.1:\d+)/#session=([A-F0-9]+)') { $address = $Matches[1]; $sessionKey = $Matches[2]; break }
        Start-Sleep -Milliseconds 200
    }
    if (!$address) { throw 'Portable process did not start within 20 seconds' }
    $headers = @{ 'X-Lens-Session' = $sessionKey }
    $unauthorized = Invoke-WebRequest -Uri "$address/api/demo" -SkipHttpErrorCheck
    Assert-Lens ($unauthorized.StatusCode -eq 403) 'API accepted a request without the local session key'
    $origin = Invoke-WebRequest -Uri "$address/api/demo" -Headers @{ 'X-Lens-Session' = $sessionKey; Origin = 'https://example.invalid' } -SkipHttpErrorCheck
    Assert-Lens ($origin.StatusCode -eq 403) 'API accepted a foreign browser origin'
    $hostCheck = Invoke-WebRequest -Uri "$address/" -Headers @{ Host = 'example.invalid' } -SkipHttpErrorCheck
    Assert-Lens ($hostCheck.StatusCode -eq 403) 'Host header validation failed'
    $html = Invoke-WebRequest -Uri "$address/"
    Assert-Lens ($html.StatusCode -eq 200 -and $html.Content -match 'Component Lens') 'Embedded HTML did not load'
    Assert-Lens ($html.Headers['Content-Security-Policy'] -match "script-src 'self'") 'Content Security Policy missing'
    foreach ($asset in 'app.mjs','core.mjs','i18n.mjs','style.css','favicon.svg') {
        $response = Invoke-WebRequest -Uri "$address/$asset"
        Assert-Lens ($response.StatusCode -eq 200 -and $response.RawContentLength -gt 10) "Embedded asset failed: $asset"
    }
    $demo = Invoke-RestMethod -Uri "$address/api/demo" -Headers $headers
    Assert-Lens ($demo.demo -eq $true -and $demo.summary.crossGroups -eq 4 -and $demo.summary.missingIdentity -eq 2) 'Demo comparison is incorrect'
    Assert-Lens (!(ConvertTo-Json $demo -Depth 30).Contains('test-token')) 'Report unexpectedly includes credentials'
    $sessionInfo = Invoke-RestMethod -Uri "$address/api/session" -Headers $headers
    Assert-Lens ($null -eq $sessionInfo.a -and $null -eq $sessionInfo.b -and $null -eq $sessionInfo.job) 'Unexpected initial session state'
    $scan = Invoke-WebRequest -Uri "$address/api/scans" -Method Post -ContentType 'application/json' -Headers $headers -Body '{"ignoreCase":true}' -SkipHttpErrorCheck
    Assert-Lens ($scan.StatusCode -eq 400 -and ($scan.Content | ConvertFrom-Json).error -eq 'connectBoth') 'Scan without workspaces was not rejected'
    $invalid = Invoke-WebRequest -Uri "$address/api/workspaces/a/connect" -Method Post -ContentType 'application/json' -Headers $headers -Body '{"name":"Test","endpoint":"https://example.invalid","token":"test-token"}' -SkipHttpErrorCheck
    Assert-Lens ($invalid.StatusCode -eq 400 -and ($invalid.Content | ConvertFrom-Json).error -eq 'invalidEndpoint') 'Invalid external endpoint was accepted'
    Invoke-RestMethod -Uri "$address/api/quit" -Method Post -Headers $headers | Out-Null
    Assert-Lens ($process.WaitForExit(10000)) 'Quit did not terminate the portable server'
    Assert-Lens ($process.ExitCode -eq 0) 'Portable server exited with an error'
    Write-Output "Portable smoke test: $script:assertions assertions passed. Embedded runtime, assets, API, host/origin checks and shutdown verified."
} finally {
    $env:DOTNET_ROOT = $previousRoot
    $env:DOTNET_MULTILEVEL_LOOKUP = $previousLookup
    if ($process -and !$process.HasExited) { Stop-Process -Id $process.Id }
    # The stdout contains a local, per-process session key. Remove only these two known files.
    Remove-Item -LiteralPath $stdoutPath,$stderrPath -Force -ErrorAction SilentlyContinue
}
