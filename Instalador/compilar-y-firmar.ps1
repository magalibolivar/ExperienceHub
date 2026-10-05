<#
.SYNOPSIS
    Empaqueta el instalador de ExperienceHub con Inno Setup y (opcional) lo firma.

.DESCRIPTION
    1. Descarga SQL Server 2022 Express LocalDB (Redist\SqlLocalDB.msi) si falta
       (va embebido para equipos sin ningun motor de SQL).
    2. Compila ExperienceHub_Setup.iss  ->  Salida\Instalador_ExperienceHub_V1.exe
    3. Si hay certificado .pfx, firma el .exe (SignTool SHA256 + sello de tiempo).

    REQUISITO PREVIO: compilar la solucion en modo Release (GUI y DbInstaller),
    porque el .iss toma los binarios de GUI\bin\Release y DbInstaller\bin\Release.
    Por ejemplo, desde la raiz del repo:
        MSBuild "IngSoftware-Bolivar,Morana.slnx" -t:Build -p:Configuration=Release

    El certificado NO se versiona (contiene la clave privada). Por defecto se busca
    en %USERPROFILE%\experiencehub.pfx. La contrasena se toma de la variable de
    entorno EH_PFX_PASS o, si no existe, se pide por consola. Sin certificado, el
    instalador queda compilado SIN firmar (no falla).

.EXAMPLE
    .\compilar-y-firmar.ps1
    .\compilar-y-firmar.ps1 -Pfx D:\certs\otro.pfx
#>
param(
    [string]$Pfx = (Join-Path $env:USERPROFILE 'experiencehub.pfx')
)

$ErrorActionPreference = 'Stop'
$dir = $PSScriptRoot
$exe = Join-Path $dir 'Salida\Instalador_ExperienceHub_V1.exe'

# -- 0) SQL Server 2022 Express LocalDB (va embebido para equipos sin SQL) -----
$msi = Join-Path $dir 'Redist\SqlLocalDB.msi'
if (-not (Test-Path $msi)) {
    Write-Host 'Descargando SQL Server LocalDB...' -ForegroundColor Cyan
    New-Item -ItemType Directory -Force (Split-Path $msi) | Out-Null
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12  # PowerShell 5.1 no lo usa por defecto
    Invoke-WebRequest 'https://download.microsoft.com/download/3/8/d/38de7036-2433-4207-8eae-06e247e17b25/SqlLocalDB.msi' -OutFile $msi -UseBasicParsing
}
if ((Get-AuthenticodeSignature $msi).Status -ne 'Valid') { throw 'SqlLocalDB.msi no tiene una firma valida de Microsoft.' }

# -- 1) Compilar con Inno Setup ------------------------------------------------
$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'No se encontro Inno Setup 6 (ISCC.exe). Instalalo desde https://jrsoftware.org/isdl.php' }

Write-Host 'Compilando el instalador...' -ForegroundColor Cyan
& $iscc (Join-Path $dir 'ExperienceHub_Setup.iss') | Select-Object -Last 3
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $exe)) { throw 'Fallo la compilacion del instalador.' }

# -- 2) Firmar con SignTool (opcional) -----------------------------------------
if (-not (Test-Path $Pfx)) {
    Write-Warning "No se encontro el certificado ($Pfx): el instalador queda SIN firmar."
    Write-Host $exe
    return
}

$signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\x64\\' } | Sort-Object FullName -Descending |
    Select-Object -First 1 -ExpandProperty FullName
$pass = $env:EH_PFX_PASS
if (-not $pass) {
    $sec  = Read-Host 'Contrasena del certificado' -AsSecureString
    $pass = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec))
}

Write-Host 'Firmando el instalador...' -ForegroundColor Cyan
if ($signtool) {
    & $signtool sign /f $Pfx /p $pass /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $exe
    if ($LASTEXITCODE -ne 0) { throw 'Fallo la firma con SignTool.' }
} else {
    $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($Pfx, $pass)
    $r = Set-AuthenticodeSignature -FilePath $exe -Certificate $cert -HashAlgorithm SHA256 -TimestampServer 'http://timestamp.digicert.com'
    if (-not $r.SignerCertificate) { throw "Fallo la firma: $($r.StatusMessage)" }
}

$firma = Get-AuthenticodeSignature $exe
Write-Host ("Firmado por: {0}  |  Estado: {1}" -f $firma.SignerCertificate.Subject, $firma.Status) -ForegroundColor Green
Write-Host $exe
