# Stop old host, build, run SyllabusAI.Service (single instance on http://localhost:5070).
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

& (Join-Path $PSScriptRoot 'stop-service.ps1')

Push-Location $root
try {
    dotnet build "SyllabusAI.Service/SyllabusAI.Service.csproj" -v q
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet run --project "SyllabusAI.Service/SyllabusAI.Service.csproj" --no-build
}
finally {
    Pop-Location
}
