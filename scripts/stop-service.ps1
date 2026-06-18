# Stops SyllabusAI.Service and dotnet hosts that lock bin\Debug DLLs.
$ErrorActionPreference = 'SilentlyContinue'

Get-Process -Name 'SyllabusAI.Service' | Stop-Process -Force
Start-Sleep -Milliseconds 400

Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" |
    Where-Object { $_.CommandLine -like '*SyllabusAI.Service*' } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force }

Write-Host "Stopped SyllabusAI.Service (if it was running)."
