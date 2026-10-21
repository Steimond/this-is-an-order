$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$cacheRoot = Join-Path $taskRoot '.local-ai'
New-Item -ItemType Directory -Path $cacheRoot -Force | Out-Null

function Get-VerifiedFile([string]$Url, [string]$Path, [string]$Sha256) {
    if (Test-Path -LiteralPath $Path) {
        if ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -eq $Sha256) { return }
        throw "Checksum mismatch for existing file: $Path"
    }
    $partialPath = "$Path.download"
    Invoke-WebRequest -Uri $Url -OutFile $partialPath
    if ((Get-FileHash -LiteralPath $partialPath -Algorithm SHA256).Hash -ne $Sha256) {
        throw "Downloaded file checksum mismatch: $partialPath"
    }
    Move-Item -LiteralPath $partialPath -Destination $Path
}

$archivePath = Join-Path $cacheRoot 'llama-b11787-bin-win-cpu-x64.zip'
Get-VerifiedFile 'https://github.com/ggml-org/llama.cpp/releases/download/b11787/llama-b11787-bin-win-cpu-x64.zip' $archivePath '3f60b8c13f73acee3de80fd26d1649604867278821694c5f10e76a59b87498bb'
$runtimePath = Join-Path $cacheRoot 'llama-b11787'
if (-not (Test-Path -LiteralPath (Join-Path $runtimePath 'llama-server.exe'))) {
    Expand-Archive -LiteralPath $archivePath -DestinationPath $runtimePath -Force
}
Get-VerifiedFile 'https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct-GGUF/resolve/91cad51170dc346986eccefdc2dd33a9da36ead9/qwen2.5-1.5b-instruct-q4_k_m.gguf' (Join-Path $cacheRoot 'qwen2.5-1.5b-instruct-q4_k_m.gguf') '6a1a2eb6d15622bf3c96857206351ba97e1af16c30d7a74ee38970e434e9407e'
Write-Output 'Local AI runtime and model verified. Unity starts the local process automatically in Play Mode.'
