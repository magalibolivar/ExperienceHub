; =====================================================================
; ExperienceHub - Instalador (A01, version inicial)
;
; - Instala la aplicacion (GUI.exe + dependencias) en Archivos de Programa.
; - Crea la base de datos ExperienceHubDB completa (esquema + datos
;   iniciales de los 2 procesos de negocio) ejecutando
;   BD\00_Instalacion_Completa.sql mediante un cliente SQL embebido
;   (DbInstaller.exe, ADO.NET/SqlClient) que viaja DENTRO del propio
;   instalador - no depende de que el cliente tenga sqlcmd / SQL Server
;   Command Line Utilities instalados aparte.
; - Crea accesos directos (menu inicio + escritorio opcional).
; - Un unico .exe de salida: todo (GUI.exe + DLLs + .sql + DbInstaller.exe)
;   va comprimido adentro.
; - Verifica .NET Framework 4.7.2+ ANTES de copiar archivos; si falta,
;   avisa y no instala nada.
; - Deteccion de instancias SQL Server locales (registro de Windows,
;   nativo + WOW64). Si hay 2+, se pide elegir cual usar.
; - Si no hay NINGUNA instancia pero SQL LocalDB SI esta instalado, se usa
;   automaticamente ((localdb)\MSSQLLocalDB). Si no hay NINGUNA (ni LocalDB),
;   la opcion por defecto instala SQL Server 2022 Express LocalDB embebido
;   (Redist\SqlLocalDB.msi, msiexec /passive): "Siguiente, Siguiente" alcanza
;   aunque el equipo no tenga ningun motor. Si el usuario elige no instalarlo,
;   se muestra el link de descarga y se corta sin copiar nada.
; - Si la instancia elegida existe pero su servicio esta detenido, se intenta
;   arrancar automaticamente (ServiceController + Start()); se chequea como
;   pre-flight en el wizard y de nuevo antes de crear la BD.
; - Rollback automatico: si el servicio no arranca o falla la creacion de la
;   BD DESPUES de copiar archivos, se desinstala todo lo recien copiado
;   (preservando el log en Documentos).
; - El GUI.exe.config instalado se reescribe con el servidor realmente
;   elegido - el connection string queda embebido pero correcto para
;   CUALQUIER instancia, no solo ".\SQLEXPRESS" fijo.
; - Al desinstalar, se pregunta (por defecto "No") si tambien borrar la BD.
; - Para instalaciones desatendidas: Instalador.exe /VERYSILENT /SERVIDOR=.\SQLEXPRESS
;
; Fuera de alcance (gap conocido, no bloqueante):
; - SQL Server Express completo (servicio multiusuario) no se embebe: sin
;   motor se instala LocalDB, que alcanza para un puesto.
; - En /VERYSILENT sin ningun motor no se instala LocalDB: pasar /SERVIDOR=
;   con una instancia existente.
; =====================================================================

#define MyAppName "ExperienceHub"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Magali Bolivar"
#define MyAppExeName "GUI.exe"
#define MyDatabaseName "ExperienceHubDB"
; Valor por defecto sugerido en la pantalla de ingreso manual del servidor.
#define MySqlInstanceSugerido ".\SQLEXPRESS"

#define AppSourceDir "..\GUI\bin\Release"
#define DbSourceDir "..\BD"
#define DbInstallerSourceDir "DbInstaller\bin\Release"
#define DbInstallerExeName "DbInstaller.exe"
; Instalador oficial de SQL Server 2022 Express LocalDB. No se versiona (es de
; Microsoft, ~60MB): compilar-y-firmar.ps1 lo descarga si falta.
#define LocalDbMsi "Redist\SqlLocalDB.msi"

#if !FileExists(AppSourceDir + "\" + MyAppExeName)
  #error "No se encontro GUI.exe en GUI\bin\Release. Compilar el proyecto en modo Release antes de generar el instalador."
#endif

#if !FileExists(DbSourceDir + "\00_Instalacion_Completa.sql")
  #error "No se encontro BD\00_Instalacion_Completa.sql (script unico de instalacion)."
#endif

#if !FileExists(DbInstallerSourceDir + "\" + DbInstallerExeName)
  #error "No se encontro DbInstaller.exe en Instalador\DbInstaller\bin\Release. Compilar ese proyecto en modo Release antes de generar el instalador."
#endif

#if !FileExists(LocalDbMsi)
  #error "No se encontro Redist\SqlLocalDB.msi. Correr compilar-y-firmar.ps1, que lo descarga."
#endif

[Setup]
AppId={{A7E4C2D1-5B3F-4A8E-9C6D-1E2F3A4B5C6D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
; Sin esto, un instalador de 32 bits (default de Inno) en Windows de 64 bits
; queda sujeto a la redireccion WOW64 del registro y NUNCA encuentra las
; instancias de SQL Server (que se registran en la rama nativa de 64 bits).
ArchitecturesInstallIn64BitMode=x64compatible
; Si GUI.exe esta corriendo, Inno detecta la app abierta (Restart Manager) y
; pide cerrarla antes de continuar, en vez de fallar con el archivo bloqueado.
CloseApplications=yes
RestartApplications=no
OutputDir=Salida
OutputBaseFilename=Instalador_ExperienceHub_V1
Compression=lzma
SolidCompression=yes
WizardStyle=modern
SetupIconFile={#AppSourceDir}\icon.ico
UninstallDisplayName={#MyAppName}

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; GroupDescription: "Accesos directos adicionales:"; Flags: unchecked

[Files]
Source: "{#AppSourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#DbSourceDir}\00_Instalacion_Completa.sql"; DestDir: "{app}\BD"; Flags: ignoreversion
Source: "{#DbInstallerSourceDir}\{#DbInstallerExeName}"; DestDir: "{app}\BD"; Flags: ignoreversion
; Segunda copia, solo para {tmp}: permite usar DbInstaller.exe DURANTE el
; wizard (pre-flight de conexion) antes de que se copien los archivos a {app}.
Source: "{#DbInstallerSourceDir}\{#DbInstallerExeName}"; DestDir: "{tmp}"; Flags: dontcopy
Source: "Credenciales_Iniciales.txt"; DestDir: "{app}"; Flags: ignoreversion
; Solo se extrae (a {tmp}) si hay que instalar LocalDB: ver InstalarLocalDb.
Source: "{#LocalDbMsi}"; DestDir: "{tmp}"; Flags: dontcopy nocompression

; La app usa {app}\Backups como carpeta de respaldos; {app} esta en Archivos de
; Programa, donde un usuario comun no puede escribir sin estos permisos.
[Dirs]
Name: "{app}\Backups"; Permissions: users-modify
Name: "{app}\TempBackups"; Permissions: users-modify

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Credenciales iniciales"; Filename: "{app}\Credenciales_Iniciales.txt"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\Credenciales_Iniciales.txt"; Description: "Ver las credenciales iniciales"; Flags: postinstall shellexec skipifsilent unchecked
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir {#MyAppName}"; Flags: nowait postinstall skipifsilent

; install.log y uninstall.log se escriben en tiempo de ejecucion; sin esta
; seccion el desinstalador no borraria {app} por quedar archivos huerfanos.
[UninstallDelete]
Type: files; Name: "{app}\install.log"
Type: files; Name: "{app}\uninstall.log"

[Code]
var
  PaginaSeleccionInstancia: TInputOptionWizardPage;
  PaginaSinInstancias: TInputOptionWizardPage;
  PaginaIngresoManual: TInputQueryWizardPage;
  InstanciasDetectadas: TArrayOfString;
  LocalDbDisponible: Boolean;
  ServidorElegido: String;
  ServicioWindowsElegido: String;
  ServidorPorParametro: String;

procedure ExitProcess(uExitCode: Cardinal);
  external 'ExitProcess@kernel32.dll stdcall';

function TieneNetFramework472(): Boolean;
var
  Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release)
            and (Release >= 461808);
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  if not TieneNetFramework472() then
  begin
    SuppressibleMsgBox(
      'ExperienceHub requiere .NET Framework 4.7.2 o superior, que no se encontro instalado en este equipo.' + #13#13 +
      'Instala .NET Framework 4.7.2 (o posterior) desde ' +
      'https://dotnet.microsoft.com/download/dotnet-framework y volve a ejecutar este instalador.',
      mbError, MB_OK, IDOK);
    Result := False;
  end;
end;

function ArrayContieneTexto(const Arr: TArrayOfString; const Valor: String): Boolean;
var
  I: Integer;
begin
  Result := False;
  for I := 0 to GetArrayLength(Arr) - 1 do
    if CompareText(Arr[I], Valor) = 0 then
    begin
      Result := True;
      exit;
    end;
end;

function DetectarInstanciasSql(): TArrayOfString;
var
  NombresNativo, NombresWow: TArrayOfString;
  Combinadas: TArrayOfString;
  I, N: Integer;
begin
  if not RegGetValueNames(HKLM, 'SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL', NombresNativo) then
    SetArrayLength(NombresNativo, 0);
  if not RegGetValueNames(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\Microsoft SQL Server\Instance Names\SQL', NombresWow) then
    SetArrayLength(NombresWow, 0);

  SetArrayLength(Combinadas, GetArrayLength(NombresNativo) + GetArrayLength(NombresWow));
  N := 0;
  for I := 0 to GetArrayLength(NombresNativo) - 1 do
  begin
    Combinadas[N] := NombresNativo[I];
    N := N + 1;
  end;
  for I := 0 to GetArrayLength(NombresWow) - 1 do
  begin
    if not ArrayContieneTexto(NombresNativo, NombresWow[I]) then
    begin
      Combinadas[N] := NombresWow[I];
      N := N + 1;
    end;
  end;
  SetArrayLength(Combinadas, N);

  Result := Combinadas;
end;

function TieneLocalDbInstalado(): Boolean;
var
  Versiones: TArrayOfString;
begin
  Result := (RegGetSubkeyNames(HKLM, 'SOFTWARE\Microsoft\Microsoft SQL Server Local DB\Installed Versions', Versiones)
             and (GetArrayLength(Versiones) > 0))
         or (RegGetSubkeyNames(HKCU, 'SOFTWARE\Microsoft\Microsoft SQL Server Local DB\Installed Versions', Versiones)
             and (GetArrayLength(Versiones) > 0));
end;

function NombreServicioParaInstancia(const Instancia: String): String;
begin
  if CompareText(Instancia, 'MSSQLSERVER') = 0 then
    Result := 'MSSQLSERVER'
  else
    Result := 'MSSQL$' + Instancia;
end;

function DataSourceParaInstancia(const Instancia: String): String;
begin
  if CompareText(Instancia, 'MSSQLSERVER') = 0 then
    Result := '.'
  else
    Result := '.\' + Instancia;
end;

function VersionDeInstancia(const Instancia: String): String;
var
  Id, Mayor: String;
  P: Integer;
begin
  Result := '';
  if not RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL', Instancia, Id) then
    if not RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\Microsoft SQL Server\Instance Names\SQL', Instancia, Id) then
      exit;
  P := Pos('.', Id);
  if (Pos('MSSQL', Id) <> 1) or (P = 0) then exit;
  Mayor := Copy(Id, 6, P - 6);
  if Mayor = '11' then Result := 'SQL Server 2012'
  else if Mayor = '12' then Result := 'SQL Server 2014'
  else if Mayor = '13' then Result := 'SQL Server 2016'
  else if Mayor = '14' then Result := 'SQL Server 2017'
  else if Mayor = '15' then Result := 'SQL Server 2019'
  else if Mayor = '16' then Result := 'SQL Server 2022'
  else if Mayor = '17' then Result := 'SQL Server 2025'
  else Result := 'SQL Server v' + Mayor;
end;

function EtiquetaInstancia(const Instancia: String): String;
var
  Version: String;
begin
  Result := DataSourceParaInstancia(Instancia);
  Version := VersionDeInstancia(Instancia);
  if Version <> '' then
    Result := Result + '   (' + Version + ')';
end;

function EsLocalDb(const Servidor: String): Boolean;
begin
  Result := Pos('(localdb)', Lowercase(Servidor)) = 1;
end;

function IndiceLocalDbEnSeleccion(): Integer;
begin
  if LocalDbDisponible then Result := GetArrayLength(InstanciasDetectadas) else Result := -1;
end;

function IndiceOtraEnSeleccion(): Integer;
begin
  Result := GetArrayLength(InstanciasDetectadas);
  if LocalDbDisponible then Result := Result + 1;
end;

function IndiceLocalDbEnSinInstancias(): Integer;
begin
  Result := 0;
end;

function IndiceManualEnSinInstancias(): Integer;
begin
  Result := 1;
end;

function IndiceCancelarEnSinInstancias(): Integer;
begin
  Result := 2;
end;

function InstalarLocalDb(): Boolean;
var
  ResultCode: Integer;
  Msi, Log, SqlLocalDbExe: String;
begin
  Result := False;
  WizardForm.NextButton.Enabled := False;
  try
    ExtractTemporaryFile('SqlLocalDB.msi');
    Msi := ExpandConstant('{tmp}\SqlLocalDB.msi');
    Log := ExpandConstant('{userdocs}\ExperienceHub_LocalDB_install.log');
    if not Exec(ExpandConstant('{sys}\msiexec.exe'),
                '/i "' + Msi + '" /passive /norestart IACCEPTSQLLOCALDBLICENSETERMS=YES /l*v "' + Log + '"',
                '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
      ResultCode := -1;
    if (ResultCode <> 0) and (ResultCode <> 3010) then
    begin
      SuppressibleMsgBox(
        'No se pudo instalar SQL Server LocalDB (codigo ' + IntToStr(ResultCode) + ').' + #13#13 +
        'Detalle en: ' + Log + #13#13 +
        'Podes instalar SQL Server Express a mano desde https://www.microsoft.com/sql-server/sql-server-downloads ' +
        'y volver a ejecutar este instalador. No se instalo nada de ExperienceHub.',
        mbError, MB_OK, IDOK);
      exit;
    end;

    LocalDbDisponible := TieneLocalDbInstalado();
    if not LocalDbDisponible then
    begin
      SuppressibleMsgBox('SQL Server LocalDB se instalo pero no aparece registrado en el equipo. Detalle en: ' + Log,
                         mbError, MB_OK, IDOK);
      exit;
    end;

    SqlLocalDbExe := ExpandConstant('{commonpf64}\Microsoft SQL Server\160\Tools\Binn\SqlLocalDB.exe');
    if FileExists(SqlLocalDbExe) then
    begin
      Exec(SqlLocalDbExe, 'create MSSQLLocalDB', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
      Exec(SqlLocalDbExe, 'start MSSQLLocalDB', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    end;
    Result := True;
  finally
    WizardForm.NextButton.Enabled := True;
  end;
end;

procedure InitializeWizard();
var
  I, IndicePrevio: Integer;
  ServidorPrevio: String;
begin
  InstanciasDetectadas := DetectarInstanciasSql();
  LocalDbDisponible := TieneLocalDbInstalado();
  ServidorPorParametro := Trim(ExpandConstant('{param:SERVIDOR|}'));
  ServidorPrevio := GetPreviousData('ServidorSql', '');

  if GetArrayLength(InstanciasDetectadas) > 0 then
  begin
    ServidorElegido := DataSourceParaInstancia(InstanciasDetectadas[0]);
    ServicioWindowsElegido := NombreServicioParaInstancia(InstanciasDetectadas[0]);
  end
  else if LocalDbDisponible then
  begin
    ServidorElegido := '(localdb)\MSSQLLocalDB';
    ServicioWindowsElegido := '';
  end
  else
  begin
    ServidorElegido := '{#MySqlInstanceSugerido}';
    ServicioWindowsElegido := '';
  end;

  try
    ExtractTemporaryFile('{#DbInstallerExeName}');
  except
  end;

  PaginaSeleccionInstancia := CreateInputOptionPage(wpSelectTasks,
    'Base de datos', 'Se encontro SQL Server en este equipo',
    'Se detectaron las siguientes instancias de SQL Server instaladas localmente. Elegi cual va a usar ExperienceHub:',
    True, False);
  IndicePrevio := -1;
  for I := 0 to GetArrayLength(InstanciasDetectadas) - 1 do
  begin
    PaginaSeleccionInstancia.Add(EtiquetaInstancia(InstanciasDetectadas[I]));
    if CompareText(DataSourceParaInstancia(InstanciasDetectadas[I]), ServidorPrevio) = 0 then
      IndicePrevio := I;
  end;
  if LocalDbDisponible then
  begin
    PaginaSeleccionInstancia.Add('(localdb)\MSSQLLocalDB   (SQL LocalDB)');
    if EsLocalDb(ServidorPrevio) then
      IndicePrevio := IndiceLocalDbEnSeleccion();
  end;
  PaginaSeleccionInstancia.Add('Otra instancia o servidor (ingresar manualmente)');
  if (IndicePrevio = -1) and (ServidorPrevio <> '') then
    IndicePrevio := IndiceOtraEnSeleccion();
  if GetArrayLength(InstanciasDetectadas) > 0 then
  begin
    if IndicePrevio >= 0 then
      PaginaSeleccionInstancia.SelectedValueIndex := IndicePrevio
    else
      PaginaSeleccionInstancia.SelectedValueIndex := 0;
  end;

  PaginaSinInstancias := CreateInputOptionPage(PaginaSeleccionInstancia.ID,
    'Base de datos', 'No se encontro SQL Server en este equipo',
    'ExperienceHub necesita una instancia de SQL Server (Express, LocalDB, o una instalacion completa) para funcionar. Como queres continuar?',
    True, False);
  if LocalDbDisponible then
    PaginaSinInstancias.Add('Usar SQL LocalDB, ya detectado en este equipo ((localdb)\MSSQLLocalDB)')
  else
    PaginaSinInstancias.Add('Instalar SQL Server Express LocalDB ahora (recomendado, incluido en este instalador)');
  PaginaSinInstancias.Add('Ya tengo SQL Server instalado (con otro nombre, o en otro servidor) - ingresar manualmente');
  PaginaSinInstancias.Add('No tengo ningun motor de SQL Server instalado - cancelar la instalacion');
  PaginaSinInstancias.SelectedValueIndex := 0;

  PaginaIngresoManual := CreateInputQueryPage(PaginaSinInstancias.ID,
    'Base de datos', 'Servidor de SQL Server',
    'Ingresa el servidor y, si corresponde, la instancia (ejemplos: .\SQLEXPRESS, (localdb)\MSSQLLocalDB, MIPC\INSTANCIA):');
  PaginaIngresoManual.Add('Servidor\Instancia:', False);
  PaginaIngresoManual.Values[0] := '{#MySqlInstanceSugerido}';
  if ServidorPrevio <> '' then
    PaginaIngresoManual.Values[0] := ServidorPrevio;

  if (GetArrayLength(InstanciasDetectadas) > 0) and (IndicePrevio >= 0) then
  begin
    if IndicePrevio < GetArrayLength(InstanciasDetectadas) then
    begin
      ServidorElegido := DataSourceParaInstancia(InstanciasDetectadas[IndicePrevio]);
      ServicioWindowsElegido := NombreServicioParaInstancia(InstanciasDetectadas[IndicePrevio]);
    end
    else
    begin
      ServidorElegido := ServidorPrevio;
      ServicioWindowsElegido := '';
    end;
  end;

  if ServidorPorParametro <> '' then
  begin
    ServidorElegido := ServidorPorParametro;
    ServicioWindowsElegido := '';
    for I := 0 to GetArrayLength(InstanciasDetectadas) - 1 do
      if CompareText(DataSourceParaInstancia(InstanciasDetectadas[I]), ServidorPorParametro) = 0 then
        ServicioWindowsElegido := NombreServicioParaInstancia(InstanciasDetectadas[I]);
  end;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  if (ServidorPorParametro <> '') and ((PageID = PaginaSeleccionInstancia.ID) or
     (PageID = PaginaSinInstancias.ID) or (PageID = PaginaIngresoManual.ID)) then
    Result := True
  else if PageID = PaginaSeleccionInstancia.ID then
    Result := GetArrayLength(InstanciasDetectadas) = 0
  else if PageID = PaginaSinInstancias.ID then
    Result := GetArrayLength(InstanciasDetectadas) > 0
  else if PageID = PaginaIngresoManual.ID then
  begin
    if GetArrayLength(InstanciasDetectadas) > 0 then
      Result := PaginaSeleccionInstancia.SelectedValueIndex <> IndiceOtraEnSeleccion()
    else
      Result := PaginaSinInstancias.SelectedValueIndex <> IndiceManualEnSinInstancias();
  end;
end;

function VerificarServicioPreflight(const NombreServicio: String): Boolean;
var
  ResultCode: Integer;
  DbInstallerTmp, LogPath: String;
begin
  Result := True;
  DbInstallerTmp := ExpandConstant('{tmp}\{#DbInstallerExeName}');
  if not FileExists(DbInstallerTmp) then exit;

  LogPath := ExpandConstant('{tmp}\preflight.log');
  if Exec(DbInstallerTmp, 'check-service "' + NombreServicio + '" "' + LogPath + '"',
          '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0) then
    exit;

  if SuppressibleMsgBox(
       'El servicio de Windows para esa instancia (' + NombreServicio + ') no esta disponible ' +
       '(no existe, o esta detenido y no se pudo iniciar automaticamente - faltan permisos de administrador?).' + #13#13 +
       'Queres continuar de todos modos? (se va a reintentar mas adelante)',
       mbConfirmation, MB_YESNO, IDYES) = IDNO then
    Result := False;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  ResultCode: Integer;
  LogPath: String;
  DbInstallerTmp: String;
  IndiceElegido: Integer;
begin
  Result := True;

  if CurPageID = PaginaSeleccionInstancia.ID then
  begin
    IndiceElegido := PaginaSeleccionInstancia.SelectedValueIndex;
    if IndiceElegido < GetArrayLength(InstanciasDetectadas) then
    begin
      ServidorElegido := DataSourceParaInstancia(InstanciasDetectadas[IndiceElegido]);
      ServicioWindowsElegido := NombreServicioParaInstancia(InstanciasDetectadas[IndiceElegido]);
      if not VerificarServicioPreflight(ServicioWindowsElegido) then
      begin
        Result := False;
        exit;
      end;
    end
    else if IndiceElegido = IndiceLocalDbEnSeleccion() then
    begin
      ServidorElegido := '(localdb)\MSSQLLocalDB';
      ServicioWindowsElegido := '';
    end;
  end;

  if CurPageID = PaginaSinInstancias.ID then
  begin
    if PaginaSinInstancias.SelectedValueIndex = IndiceLocalDbEnSinInstancias() then
    begin
      if not LocalDbDisponible then
        if not InstalarLocalDb() then
        begin
          Result := False;
          exit;
        end;
      ServidorElegido := '(localdb)\MSSQLLocalDB';
      ServicioWindowsElegido := '';
    end
    else if PaginaSinInstancias.SelectedValueIndex = IndiceCancelarEnSinInstancias() then
    begin
      SuppressibleMsgBox(
        'ExperienceHub necesita SQL Server para funcionar y no se detecto ninguna instancia en este equipo.' + #13#13 +
        'Descarga e instala SQL Server Express (o LocalDB) desde:' + #13 +
        'https://www.microsoft.com/sql-server/sql-server-downloads' + #13#13 +
        'Despues, volve a ejecutar este instalador. No se instalo nada.',
        mbInformation, MB_OK, IDOK);
      ExitProcess(1);
    end;
  end;

  if CurPageID = PaginaIngresoManual.ID then
  begin
    ServidorElegido := Trim(PaginaIngresoManual.Values[0]);
    if ServidorElegido = '' then
    begin
      SuppressibleMsgBox('Ingresa un servidor/instancia valido.', mbError, MB_OK, IDOK);
      Result := False;
      exit;
    end;

    ServicioWindowsElegido := '';

    DbInstallerTmp := ExpandConstant('{tmp}\{#DbInstallerExeName}');
    if FileExists(DbInstallerTmp) then
    begin
      WizardForm.StatusLabel.Caption := 'Probando conexion...';
      LogPath := ExpandConstant('{tmp}\preflight.log');
      if not (Exec(DbInstallerTmp, 'test-connection "' + ServidorElegido + '" "' + LogPath + '"',
                    '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0)) then
      begin
        if SuppressibleMsgBox(
             'No se pudo conectar a ''' + ServidorElegido + '''.' + #13#13 +
             'Verifica el nombre del servidor/instancia y que el servicio este en ejecucion.' + #13#13 +
             'Queres continuar de todos modos?',
             mbConfirmation, MB_YESNO, IDNO) = IDNO then
        begin
          Result := False;
          exit;
        end;
      end;
    end;
  end;
end;

function PosDesde(const SubStr, S: AnsiString; Desde: Integer): Integer;
var
  I, Len: Integer;
begin
  Result := 0;
  Len := Length(SubStr);
  if Len = 0 then exit;
  for I := Desde to Length(S) - Len + 1 do
  begin
    if Copy(S, I, Len) = SubStr then
    begin
      Result := I;
      exit;
    end;
  end;
end;

function ReemplazarDataSource(const Contenido, NuevoServidor: AnsiString): AnsiString;
var
  Inicio, Fin: Integer;
  Marca: AnsiString;
begin
  Result := Contenido;
  Marca := 'Data Source=';
  Inicio := Pos(Marca, Contenido);
  if Inicio = 0 then exit;
  Inicio := Inicio + Length(Marca);
  Fin := PosDesde(';', Contenido, Inicio);
  if Fin = 0 then exit;
  Result := Copy(Contenido, 1, Inicio - 1) + NuevoServidor + Copy(Contenido, Fin, Length(Contenido) - Fin + 1);
end;

procedure ActualizarConnectionString(const NuevoServidor: String);
var
  RutaConfig: String;
  Contenido: AnsiString;
begin
  RutaConfig := ExpandConstant('{app}\{#MyAppExeName}.config');
  if LoadStringFromFile(RutaConfig, Contenido) then
    SaveStringToFile(RutaConfig, ReemplazarDataSource(Contenido, AnsiString(NuevoServidor)), False);
end;

function EjecutarScriptSql(NombreArchivo: String; var ErrMsg: String): Boolean;
var
  ResultCode: Integer;
  DbInstallerPath, ScriptPath, LogPath, Params: String;
begin
  DbInstallerPath := ExpandConstant('{app}\BD\{#DbInstallerExeName}');
  ScriptPath := ExpandConstant('{app}\BD\') + NombreArchivo;
  LogPath := ExpandConstant('{app}\install.log');

  Params := 'run-script "' + ServidorElegido + '" "' + ScriptPath + '" "' + LogPath + '"';

  Result := Exec(DbInstallerPath, Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  if not Result then
  begin
    ErrMsg := 'No se pudo ejecutar DbInstaller.exe (cliente SQL embebido del instalador).';
    exit;
  end;

  if ResultCode <> 0 then
  begin
    Result := False;
    ErrMsg := 'El script ' + NombreArchivo + ' devolvio un error (codigo ' + IntToStr(ResultCode) + ') contra ''' + ServidorElegido + '''.' + #13#13 +
              'Puede ser un problema de conexion o un error en el script SQL. Revisa el log para mas detalle: ' + LogPath;
  end;
end;

function UltimosCaracteres(const Texto: AnsiString; MaxChars: Integer): AnsiString;
begin
  if Length(Texto) <= MaxChars then
    Result := Texto
  else
    Result := '(...)' + #13#10 + Copy(Texto, Length(Texto) - MaxChars + 1, MaxChars);
end;

procedure RevertirInstalacion(const MensajeError: String; const PuedeHaberBDParcial: Boolean);
var
  LogPreservado, ExtractoLog, MensajeFinal: String;
  ContenidoLog: AnsiString;
  LogOriginal: String;
  ResultCode: Integer;
begin
  LogOriginal := ExpandConstant('{app}\install.log');
  LogPreservado := ExpandConstant('{userdocs}\ExperienceHub_instalacion_fallida.log');
  ExtractoLog := '';

  if FileExists(LogOriginal) then
  begin
    if LoadStringFromFile(LogOriginal, ContenidoLog) then
      ExtractoLog := UltimosCaracteres(ContenidoLog, 500);
    if not CopyFile(LogOriginal, LogPreservado, False) then
      LogPreservado := '(no se pudo guardar una copia del log; el detalle completo se perdio junto con la instalacion)';
  end
  else
    LogPreservado := '(no se genero ningun log para este error)';

  MensajeFinal :=
    'QUE PASO:' + #13#10 + MensajeError + #13#13;

  if PuedeHaberBDParcial then
    MensajeFinal := MensajeFinal +
      'Como el script de creacion de la base no se ejecuta dentro de una unica transaccion, ' +
      'es posible que hayan quedado objetos creados a medias en el servidor.' + #13#13;

  MensajeFinal := MensajeFinal +
    'QUE SE HIZO:' + #13#10 +
    'Se deshace la instalacion (rollback automatico) para no dejar la aplicacion instalada pero no funcional.' + #13#13 +
    'QUE HACER AHORA:' + #13#10 +
    'Soluciona el problema descripto arriba y volve a ejecutar el instalador. ' +
    'Log completo: ' + LogPreservado;

  if ExtractoLog <> '' then
    MensajeFinal := MensajeFinal + #13#13 + 'DETALLE TECNICO (ultimas lineas del log):' + #13#10 + ExtractoLog;

  SuppressibleMsgBox(MensajeFinal, mbError, MB_OK, IDOK);

  Exec(ExpandConstant('{uninstallexe}'), '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  ExitProcess(1);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ErrMsg: String;
  ResultCode: Integer;
  LogPath: String;
begin
  if CurStep = ssPostInstall then
  begin
    ActualizarConnectionString(ServidorElegido);

    if ServicioWindowsElegido <> '' then
    begin
      WizardForm.StatusLabel.Caption := 'Verificando el servicio de SQL Server...';
      LogPath := ExpandConstant('{app}\install.log');
      if not Exec(ExpandConstant('{app}\BD\{#DbInstallerExeName}'),
                  'check-service "' + ServicioWindowsElegido + '" "' + LogPath + '"',
                  '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
        ResultCode := -1;

      if ResultCode = 2 then
        RevertirInstalacion(
          'No se encontro el servicio de Windows ''' + ServicioWindowsElegido + ''' en este equipo. ' +
          'La instancia de SQL Server puede haberse desinstalado.', False)
      else if ResultCode <> 0 then
        RevertirInstalacion(
          'El servicio de SQL Server (' + ServicioWindowsElegido + ') esta detenido y no se pudo iniciar ' +
          'automaticamente (faltan permisos de administrador?).', False);
    end;

    WizardForm.StatusLabel.Caption := 'Creando la base de datos ExperienceHubDB...';

    if not EjecutarScriptSql('00_Instalacion_Completa.sql', ErrMsg) then
      RevertirInstalacion('No se pudo completar la creacion de la base de datos.' + #13#13 + ErrMsg, True);

    LogPath := ExpandConstant('{app}\install.log');

    if not EsLocalDb(ServidorElegido) then
    begin
      WizardForm.StatusLabel.Caption := 'Otorgando acceso a los usuarios del equipo...';
      Exec(ExpandConstant('{app}\BD\{#DbInstallerExeName}'),
           'grant-users "' + ServidorElegido + '" "{#MyDatabaseName}" "' + LogPath + '"',
           '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    end;

    WizardForm.StatusLabel.Caption := 'Verificando la instalacion...';
    if not (Exec(ExpandConstant('{app}\BD\{#DbInstallerExeName}'),
                 'verify "' + ServidorElegido + '" "{#MyDatabaseName}" "' + LogPath + '"',
                 '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0)) then
      RevertirInstalacion('La base de datos se creo pero la verificacion final fallo ' +
                          '(no se encontro el usuario admin inicial o faltan datos).', True);
  end;
end;

procedure RegisterPreviousData(PreviousDataKey: Integer);
begin
  SetPreviousData(PreviousDataKey, 'ServidorSql', ServidorElegido);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
  LogPath, ServidorGuardado: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    if SuppressibleMsgBox(
         'Queres borrar tambien la base de datos ExperienceHubDB?' + #13#13 +
         'Esta accion NO se puede deshacer. Si no estas seguro, elegi "No": la base va a quedar en el servidor aunque desinstales la aplicacion.',
         mbConfirmation, MB_YESNO or MB_DEFBUTTON2, IDNO) = IDYES then
    begin
      ServidorGuardado := GetPreviousData('ServidorSql', '{#MySqlInstanceSugerido}');
      LogPath := ExpandConstant('{app}\uninstall.log');
      Exec(ExpandConstant('{app}\BD\{#DbInstallerExeName}'),
           'drop-database "' + ServidorGuardado + '" "{#MyDatabaseName}" "' + LogPath + '"',
           '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    end;
  end;
end;
