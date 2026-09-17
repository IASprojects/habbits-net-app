#!/usr/bin/env pwsh
# run.ps1 - Lanza los proyectos de la capa de presentacion (Api y WebBlazor).
# Uso:
#   ./run.ps1              -> compila y ejecuta Api + WebBlazor en ventanas separadas
#   ./run.ps1 -api         -> solo Api
#   ./run.ps1 -web         -> solo WebBlazor
#   ./run.ps1 -no-build    -> omite la compilacion previa
#   ./run.ps1 -wait        -> ejecuta en esta misma terminal (Ctrl+C para detener)

param(
    [switch]$api      = $false,
    [switch]$web      = $false,
    [switch]$noBuild  = $false,
    [switch]$wait     = $false
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
$apiProject = Join-Path $root "src\HabitsApp.Presentation\HabitsApp.Api\HabitsApp.Api.csproj"
$webProject = Join-Path $root "src\HabitsApp.Presentation\HabitsApp.WebBlazor\HabitsApp.WebBlazor.csproj"

$runApi = $api -or (-not $web)
$runWeb = $web -or (-not $api)

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "dotnet no esta instalado o no esta en el PATH."
    exit 1
}

if (-not $noBuild) {
    Write-Host "=== Compilando proyectos de presentacion ==="
    if ($runApi) { dotnet build $apiProject }
    if ($runWeb) { dotnet build $webProject }
    if ($LASTEXITCODE -ne 0) {
        Write-Error "La compilacion fallo. Abortando."
        exit 1
    }
}

function Get-RunArgs($csproj) {
    return @("run", "--project", $csproj)
}

if ($wait) {
    $procs = @()
    if ($runApi) {
        Write-Host "=== HabitsApp.Api (http://localhost:5170) ==="
        $procs += Start-Process -FilePath "dotnet" -ArgumentList (Get-RunArgs $apiProject) -NoNewWindow -PassThru
    }
    if ($runWeb) {
        Write-Host "=== HabitsApp.WebBlazor (http://localhost:5119) ==="
        $procs += Start-Process -FilePath "dotnet" -ArgumentList (Get-RunArgs $webProject) -NoNewWindow -PassThru
    }

    Write-Host ""
    Write-Host "Presiona Ctrl+C para detener los servidores."
    try {
        while ($procs | Where-Object { -not $_.HasExited }) {
            Sleep 2
        }
        exit 0
    }
    catch {
        foreach ($p in $procs) { if (-not $p.HasExited) { Stop-Process -Id $p.Pid -Force } }
        exit 130
    }
}

if ($runApi) {
    Write-Host "=== Lanzando HabitsApp.Api (http://localhost:5170) ==="
    Start-Process -FilePath "powershell" -ArgumentList "-NoExit", "-Command", "dotnet run --project `"$apiProject`"" -WindowStyle Normal
}
if ($runWeb) {
    Write-Host "=== Lanzando HabitsApp.WebBlazor (http://localhost:5119) ==="
    Start-Process -FilePath "powershell" -ArgumentList "-NoExit", "-Command", "dotnet run --project `"$webProject`"" -WindowStyle Normal
}

Write-Host ""
Write-Host "Procesos lanzados en ventanas separadas. Revisa cada ventana para ver los logs."