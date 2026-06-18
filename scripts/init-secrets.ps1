# Copy secrets template for local dev (secrets.Local.json is gitignored).
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$secretsDir = Join-Path $root 'config\secrets'
$target = Join-Path $secretsDir 'secrets.Local.json'
$example = Join-Path $secretsDir 'secrets.Local.example.json'

if (-not (Test-Path $example)) {
    Write-Error "Missing template: $example"
}

if (Test-Path $target) {
    Write-Host "Already exists: $target"
    exit 0
}

Copy-Item $example $target
Write-Host "Created $target — edit with your SQL server, OpenAI key, and JWT secret."
