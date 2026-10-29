param([string]$Executable = '')
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (!$Executable) { $Executable = Join-Path $repo 'dist/win-x64/ComponentLens.exe' }
$runId = [guid]::NewGuid().ToString('N')
$relativeData = ".artifacts/persistence-$runId/state"
$directory = Join-Path $repo ".artifacts/persistence-$runId"
New-Item -ItemType Directory -Force -Path $directory | Out-Null
$script:processes = @()
$script:assertions = 0
function Assert-Check($Condition, [string]$Message) { if (!$Condition) { throw $Message }; $script:assertions++ }
function Start-Lens([string]$Kind) {
    $id = [guid]::NewGuid().ToString('N')
    $stdout = Join-Path $directory "$id.out"
    $stderr = Join-Path $directory "$id.err"
    if ($Kind -eq 'fixture') {
        $file = (Get-Command dotnet).Source
        $arguments = @('tests/ComponentLens.Tests/bin/Release/net10.0/ComponentLens.Tests.dll','--fixture-server','--headless','--port','0','--data-dir',$relativeData)
    } else { $file = $Executable; $arguments = @('--headless','--port','0','--data-dir',$relativeData) }
    $process = Start-Process -FilePath $file -ArgumentList $arguments -WorkingDirectory $repo -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    $script:processes += $process
    for ($attempt = 0; $attempt -lt 100; $attempt++) {
        if ($process.HasExited) { throw 'Integration process exited before startup' }
        $output = Get-Content -LiteralPath $stdout -Raw -ErrorAction SilentlyContinue
        if ($output -match '(http://127\.0\.0\.1:\d+)/#session=([A-F0-9]+)') {
            return @{Process=$process;Address=$Matches[1];Headers=@{'X-Lens-Session'=$Matches[2]}}
        }
        Start-Sleep -Milliseconds 100
    }
    throw 'Integration process did not start within 10 seconds'
}
function Get-Data($Instance, [string]$Route) { Invoke-RestMethod -Uri ($Instance.Address + $Route) -Headers $Instance.Headers }
function Post-Data($Instance, [string]$Route, $Body = @{}) { Invoke-RestMethod -Uri ($Instance.Address + $Route) -Headers $Instance.Headers -Method Post -ContentType 'application/json' -Body (ConvertTo-Json $Body -Depth 10) }
function Stop-Lens($Instance) {
    Post-Data $Instance '/api/quit' | Out-Null
    Assert-Check ($Instance.Process.WaitForExit(10000)) 'Quit did not stop an integration process'
    Assert-Check ($Instance.Process.ExitCode -eq 0) 'Integration process failed during shutdown'
}
try {
    $first = Start-Lens 'fixture'
    foreach ($slot in 'a','b') {
        $info = Post-Data $first "/api/workspaces/$slot/connect" @{name="TEST $slot";endpoint="https://qa-$slot.365.altium.com";token="fixture-secret-$slot"}
        Assert-Check ($info.slot -eq $slot) 'Fixture workspace did not connect'
    }
    Post-Data $first '/api/preferences' @{language='es'} | Out-Null
    $job = Post-Data $first '/api/scans' @{ignoreCase=$true}
    for ($attempt = 0; $attempt -lt 200 -and $job.status -eq 'running'; $attempt++) {
        Start-Sleep -Milliseconds 100
        $job = Get-Data $first ("/api/scans/" + $job.id)
    }
    Assert-Check ($job.status -eq 'completed') 'Fixture scan failed'
    $comparison = Get-Data $first ("/api/scans/" + $job.id + '/results')
    Assert-Check ($comparison.groups.Count -eq 37 -and $comparison.summary.crossGroups -eq 37) 'Full scan did not produce 37 duplicate groups'
    Assert-Check ($comparison.summary.componentsA -eq 1323 -and $comparison.summary.componentsB -eq 1866) 'Inventory counts were truncated'
    Assert-Check ($comparison.completeness -eq 'complete') 'Stable verified undercount still produces a false partial report'
    Assert-Check ($comparison.inventories[0].verification.matches -eq $true -and $comparison.inventories[0].notices -contains 'apiCountVerified') 'Inventory was not independently verified'
    $statePath = Join-Path $repo "$relativeData/state.dat"
    Assert-Check (Test-Path -LiteralPath $statePath) 'No local state was saved'
    $stored = [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes($statePath))
    Assert-Check (!$stored.Contains('fixture-secret') -and !$stored.Contains('qa-a.365.altium.com')) 'Sensitive state was saved as plaintext'
    $date = $comparison.createdAt
    Stop-Lens $first

    $second = Start-Lens 'fixture'
    $session = Get-Data $second '/api/session'
    $restored = Get-Data $second '/api/comparison'
    Assert-Check ($session.a.restored -and $session.b.restored -and $session.savedCredentials.Count -eq 2) 'Saved connections were not restored'
    Assert-Check ($session.language -eq 'es' -and $session.comparisonRestored) 'Saved language or restored-result state was lost'
    Assert-Check ($restored.createdAt -eq $date -and $restored.groups.Count -eq 37) 'Cached comparison changed during restart'
    Assert-Check ((Get-Data $second '/test/stats').requests -eq 0) 'Restart unexpectedly contacted Altium'
    Assert-Check (!(ConvertTo-Json $session -Depth 30).Contains('fixture-secret') -and !(ConvertTo-Json $restored -Depth 50).Contains('fixture-secret')) 'API returned a stored token'
    Post-Data $second '/api/workspaces/a/connect' @{name='TEST a';endpoint='https://qa-a.365.altium.com';token=''} | Out-Null
    Assert-Check ((Get-Data $second '/api/session').hasComparison) 'Reusing a saved token erased the cached result'
    Stop-Lens $second

    $before = (Get-FileHash -Algorithm SHA256 -LiteralPath $statePath).Hash
    $portable = Start-Lens 'portable'
    $session = Get-Data $portable '/api/session'
    $restored = Get-Data $portable '/api/comparison'
    Assert-Check ($session.hasComparison -and $session.a.restored -and $restored.groups.Count -eq 37 -and $restored.createdAt -eq $date) 'Published portable executable failed to restore the encrypted comparison'
    Assert-Check ($before -eq (Get-FileHash -Algorithm SHA256 -LiteralPath $statePath).Hash) 'Startup rewrote saved data instead of loading it'
    $html = (Invoke-WebRequest -Uri ($portable.Address + '/')).Content
    Assert-Check ($html -notmatch 'id="(?:pagination|previous|next)"') 'Pagination buttons remain in the portable UI'
    Invoke-RestMethod -Uri ($portable.Address + '/api/local-data') -Headers $portable.Headers -Method Delete | Out-Null
    $forgotten = Get-Data $portable '/api/session'
    Assert-Check (!$forgotten.a -and !$forgotten.b -and !$forgotten.hasComparison) 'Forget did not clear local connections and result'
    Assert-Check (!(Test-Path -LiteralPath $statePath) -and !(Test-Path -LiteralPath "$statePath.bak")) 'Forget left encrypted credentials or backup on disk'
    Stop-Lens $portable
    $empty = Start-Lens 'portable'
    Assert-Check (!(Get-Data $empty '/api/session').hasComparison -and !(Get-Data $empty '/api/session').a) 'Forgotten state returned after restart'
    Stop-Lens $empty
    Write-Output "Persistence integration smoke: $script:assertions assertions passed. Real DPAPI, process restarts, zero startup API calls, saved-token reuse, portable cache restore and forget verified."
} finally {
    foreach ($process in $script:processes) { if (!$process.HasExited) { Stop-Process -Id $process.Id } }
    Get-ChildItem -LiteralPath $directory -File | Where-Object Extension -in '.out','.err' | ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue }
}
