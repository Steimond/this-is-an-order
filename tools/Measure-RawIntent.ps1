param([int]$Limit = 0, [int]$Threads = 2)
$ErrorActionPreference = 'Stop'
if ($Limit -lt 0 -or $Threads -lt 1 -or $Threads -gt 16) { throw 'Invalid benchmark limits.' }
$taskRoot = Split-Path -Parent $PSScriptRoot
$cacheRoot = Join-Path $taskRoot '.local-ai'
$runtimePath = Join-Path $cacheRoot 'llama-b11787'
$modelPath = Join-Path $cacheRoot 'qwen2.5-1.5b-instruct-q4_k_m.gguf'
$prompt = Get-Content -LiteralPath (Join-Path $taskRoot 'Game/Assets/Order/Voice.Local/Resources/OrderIntentPrompt.txt') -Raw
$schema = Get-Content -LiteralPath (Join-Path $taskRoot 'Game/Assets/Order/Voice.Local/Resources/OrderIntentSchema.json') -Raw | ConvertFrom-Json
$examples = @(Get-Content -LiteralPath (Join-Path $taskRoot 'Game/Assets/Order/Voice.Local/Resources/OrderIntentExamples.json') -Raw | ConvertFrom-Json)
$cases = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'intent-cases.json') -Raw | ConvertFrom-Json
if ($Limit -gt 0) { $cases = @($cases | Select-Object -First $Limit) }
$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = $listener.LocalEndpoint.Port
$listener.Stop()
$apiKey = [guid]::NewGuid().ToString('N')
$headers = @{Authorization="Bearer $apiKey"}
$serverArgs = @('--model', ('"' + $modelPath + '"'), '--host', '127.0.0.1', '--port', $port, '--api-key', $apiKey,
    '--threads', $Threads, '--threads-batch', $Threads, '--parallel', '1', '--ctx-size', '2048', '--reasoning', 'off', '--reasoning-budget', '0', '--no-webui', '--log-disable')
$proc = Start-Process -FilePath (Join-Path $runtimePath 'llama-server.exe') -ArgumentList $serverArgs -WorkingDirectory $runtimePath -WindowStyle Hidden -PassThru
try {
    $startup = [System.Diagnostics.Stopwatch]::StartNew()
    while ($true) {
        if ($proc.HasExited) { throw 'Local AI process exited during startup' }
        try { $health = Invoke-RestMethod "http://127.0.0.1:$port/health" -Headers $headers -TimeoutSec 1; if ($health.status -eq 'ok') { break } } catch { }
        if ($startup.Elapsed.TotalSeconds -gt 30) { throw 'Local AI startup timeout' }
        Start-Sleep -Milliseconds 100
    }
    $startup.Stop()
    $results = @()
    foreach ($case in $cases) {
        $screen = if ($case.screen) { $case.screen } else { 'main_menu' }
        $context = "Текущий экран: $screen.`nФраза игрока: " + $case.text
        $request = @{
            messages=@(@{role='system';content=$prompt}) + $examples + @(@{role='user';content=$context})
            temperature=0; seed=42; max_tokens=100; stream=$false; cache_prompt=$true
            chat_template_kwargs=@{enable_thinking=$false}
            response_format=@{type='json_object';schema=$schema}
        } | ConvertTo-Json -Depth 20 -Compress
        $watch = [System.Diagnostics.Stopwatch]::StartNew()
        $response = Invoke-RestMethod "http://127.0.0.1:$port/v1/chat/completions" -Method Post -Headers $headers -ContentType 'application/json; charset=utf-8' -Body ([System.Text.Encoding]::UTF8.GetBytes($request)) -TimeoutSec 30
        $decision = $response.choices[0].message.content | ConvertFrom-Json
        $passed = $response.choices[0].finish_reason -eq 'stop' -and $decision.action -eq $case.action
        if ($case.PSObject.Properties.Name -contains 'value') { $passed = $passed -and $decision.value -eq $case.value }
        if ($case.PSObject.Properties.Name -contains 'faction') { $passed = $passed -and $decision.faction -eq $case.faction }
        if ($case.target_screen) { $passed = $passed -and $decision.screen -eq $case.target_screen }
        if ($case.open_audio) { $passed = $passed -and $decision.open_audio }
        $results += [pscustomobject]@{text=$case.text;passed=$passed;milliseconds=[math]::Round($watch.Elapsed.TotalMilliseconds);prompt_tokens=$response.usage.prompt_tokens;timings=$response.timings;actual=$decision;expected=$case}
        Write-Output ("{0}: {1} ({2} ms)" -f $(if ($passed) {'PASS'} else {'FAIL'}), $case.text, $results[-1].milliseconds)
    }
    $report = [pscustomobject]@{ model='Qwen2.5-1.5B-Instruct-Q4_K_M';runtime='llama.cpp b11787';threads=$Threads;startup_ms=[math]::Round($startup.Elapsed.TotalMilliseconds);total=$results.Count;passed=@($results | Where-Object passed).Count;results=$results }
    $report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $cacheRoot 'intent-report.json') -Encoding utf8
    Write-Output ("Total: {0}/{1}" -f $report.passed,$report.total)
    # Raw model quality is diagnostic. Acceptance is tested through the complete Unity pipeline,
    # including slot grounding and domain validation (LocalIntentIntegrationTests).
} finally {
    if (-not $proc.HasExited) { $proc.Kill() }
    $proc.Dispose()
}
