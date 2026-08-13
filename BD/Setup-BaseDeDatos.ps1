<#
    ExperienceHub — Puesta en marcha de la base de datos.
    Ejecuta en orden 01 -> 02 -> 05 contra SQL Server usando sqlcmd.

    Uso (desde PowerShell, parado en la carpeta BD/):
        .\Setup-BaseDeDatos.ps1                       # usa .\SQLEXPRESS con Windows Auth
        .\Setup-BaseDeDatos.ps1 -Server "localhost"   # otro servidor
        .\Setup-BaseDeDatos.ps1 -Server "srv" -User sa -Password "xxx"

    Requisitos: sqlcmd en el PATH (viene con SQL Server / "SQL Server Command Line Utilities").
#>
param(
    [string]$Server   = ".\SQLEXPRESS",
    [string]$User     = "",
    [string]$Password = ""
)

$ErrorActionPreference = "Stop"
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Los scripts 03 y 04 son opcionales (permisos granulares / limpieza). El 05 es la migración de dominio.
$scripts = @(
    "01_Crear_BaseDeDatos.sql",
    "02_Actualizar_BaseDeDatos.sql",
    "05_Dominio_ExperienceHub.sql"
)

# Verificar sqlcmd
if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    Write-Error "No se encontró 'sqlcmd' en el PATH. Instalá 'SQL Server Command Line Utilities' o usá SSMS para correr los .sql manualmente en el orden: $($scripts -join ' -> ')."
    exit 1
}

# Argumentos de autenticación
$auth = @()
if ($User -ne "") { $auth = @("-U", $User, "-P", $Password) } else { $auth = @("-E") }  # -E = Windows Auth

foreach ($s in $scripts) {
    $path = Join-Path $dir $s
    if (-not (Test-Path $path)) { Write-Error "No existe $path"; exit 1 }
    Write-Host ">> Ejecutando $s ..." -ForegroundColor Cyan
    & sqlcmd -S $Server @auth -b -i "$path"
    if ($LASTEXITCODE -ne 0) { Write-Error "Falló $s (exit $LASTEXITCODE)."; exit $LASTEXITCODE }
    Write-Host "   OK" -ForegroundColor Green
}

Write-Host ""
Write-Host "Base ExperienceHubDB lista. Login inicial: admin / administrador1!" -ForegroundColor Green
