using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;

namespace DbInstaller
{
    // Cliente SQL embebido para el instalador de ExperienceHub (A01). Ejecuta el
    // script de instalación de la base, chequea/arranca el servicio de SQL Server,
    // prueba la conexión, verifica los datos iniciales y da acceso a los usuarios
    // locales. Usa solo ADO.NET + ServiceController (que viajan con el .NET
    // Framework que ya requiere la app): NO depende de sqlcmd ni de "SQL Server
    // Command Line Utilities" instalados aparte en la máquina del cliente.
    //
    // Uso:
    //   DbInstaller.exe run-script      <servidor> <script.sql> <log.txt>
    //   DbInstaller.exe check-service   <nombreServicio> <log.txt>
    //   DbInstaller.exe test-connection <servidor> <log.txt>
    //   DbInstaller.exe check-perms     <servidor> <nombreBD> <log.txt>
    //   DbInstaller.exe backup          <servidor> <nombreBD> <version> <log.txt>
    //   DbInstaller.exe drop-database   <servidor> <nombreBD> <log.txt>
    //   DbInstaller.exe grant-users     <servidor> <nombreBD> <log.txt>
    //   DbInstaller.exe verify          <servidor> <nombreBD> <log.txt>
    //
    // Además del log (que se acumula), cada ejecución deja en "<log.txt>.out" (se
    // sobrescribe) un resultado corto para que el instalador lo muestre en pantalla:
    // la ruta del backup (backup) o el motivo de un rechazo (check-perms).
    //
    // Códigos de salida (comunes a todos los subcomandos):
    //   0 = OK   1 = uso incorrecto   2/3 = falló la operación (ver log)
    //   4 = backup: la base no existe (no hay nada que respaldar)
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine(Uso);
                return 1;
            }

            string logPath = args[args.Length - 1];
            var log = new StringBuilder();
            var salida = new StringBuilder();
            Action<string> registrar = mensaje =>
                log.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {mensaje}");
            Action<string> informar = mensaje => salida.AppendLine(mensaje);

            int resultado;
            try
            {
                switch (args[0])
                {
                    case "run-script" when args.Length == 4:
                        resultado = EjecutarScript(args[1], args[2], registrar);
                        break;
                    case "check-service" when args.Length == 3:
                        resultado = ChequearYArrancarServicio(args[1], registrar);
                        break;
                    case "test-connection" when args.Length == 3:
                        resultado = ProbarConexion(args[1], registrar);
                        break;
                    case "check-perms" when args.Length == 4:
                        resultado = ChequearPermisos(args[1], args[2], registrar, informar);
                        break;
                    case "backup" when args.Length == 5:
                        resultado = RespaldarBaseDeDatos(args[1], args[2], args[3], registrar, informar);
                        break;
                    case "drop-database" when args.Length == 4:
                        resultado = BorrarBaseDeDatos(args[1], args[2], registrar);
                        break;
                    case "grant-users" when args.Length == 4:
                        resultado = OtorgarAccesoUsuariosLocales(args[1], args[2], registrar);
                        break;
                    case "verify" when args.Length == 4:
                        resultado = VerificarInstalacion(args[1], args[2], registrar);
                        break;
                    default:
                        Console.Error.WriteLine(Uso);
                        return 1;
                }
            }
            catch (Exception ex)
            {
                registrar("ERROR inesperado: " + ex);
                resultado = 2;
            }
            finally
            {
                try
                {
                    File.AppendAllText(logPath, log.ToString(), Encoding.UTF8);
                }
                catch
                {
                    // Si no se puede escribir el log no hay nada más que hacer acá;
                    // el código de salida ya le informa al instalador si falló.
                }
                try
                {
                    // Se escribe siempre (aunque quede vacío) para que el instalador no
                    // lea el resultado de una ejecución anterior.
                    File.WriteAllText(logPath + ".out", salida.ToString(), new UTF8Encoding(false));
                }
                catch
                {
                    // Igual que el log: el código de salida es lo que manda.
                }
            }

            return resultado;
        }

        private const string Uso =
            "Uso:\n" +
            "  DbInstaller.exe run-script      <servidor> <script.sql> <log.txt>\n" +
            "  DbInstaller.exe check-service   <nombreServicio> <log.txt>\n" +
            "  DbInstaller.exe test-connection <servidor> <log.txt>\n" +
            "  DbInstaller.exe check-perms     <servidor> <nombreBD> <log.txt>\n" +
            "  DbInstaller.exe backup          <servidor> <nombreBD> <version> <log.txt>\n" +
            "  DbInstaller.exe drop-database   <servidor> <nombreBD> <log.txt>\n" +
            "  DbInstaller.exe grant-users     <servidor> <nombreBD> <log.txt>\n" +
            "  DbInstaller.exe verify          <servidor> <nombreBD> <log.txt>";

        private static int EjecutarScript(string servidor, string scriptPath, Action<string> registrar)
        {
            if (!File.Exists(scriptPath))
            {
                registrar($"ERROR: no se encontró el script '{scriptPath}'.");
                return 2;
            }

            // UTF-8 explícito: el script tiene acentos/ñ en comentarios y datos. ReadAllText
            // descarta un BOM inicial si lo hubiera; el script se mantiene sin BOM.
            string script = File.ReadAllText(scriptPath, Encoding.UTF8);
            string[] batches = SplitEnBatches(script);

            registrar($"Conectando a '{servidor}'...");
            using (var conexion = new SqlConnection(CadenaConexion(servidor)))
            {
                conexion.Open();
                registrar($"Conexión establecida. Ejecutando script ({batches.Length} batches)...");

                for (int i = 0; i < batches.Length; i++)
                {
                    string batch = batches[i].Trim();
                    if (batch.Length == 0) continue;

                    using (var cmd = new SqlCommand(batch, conexion))
                    {
                        cmd.CommandTimeout = 120;
                        try
                        {
                            cmd.ExecuteNonQuery();
                        }
                        catch (SqlException ex)
                        {
                            registrar($"ERROR en batch {i + 1}/{batches.Length}: {ex.Message}");
                            // Class 14 = SQL Server clasifica ahí los errores de permisos
                            // (CREATE DATABASE denegado, SELECT denegado, etc.) — distinguirlo
                            // de un error de sintaxis/dato evita que el usuario pierda tiempo
                            // revisando el script cuando el problema real es de permisos.
                            if (ex.Class == 14)
                                registrar("DIAGNÓSTICO: parece un problema de PERMISOS — el usuario de Windows/SQL " +
                                          "usado para instalar no tiene privilegios suficientes en esta instancia " +
                                          "(hace falta ser sysadmin, o al menos dbcreator, para crear la base).");
                            registrar("--- Contenido del batch que falló ---");
                            registrar(batch);
                            return 2;
                        }
                    }
                }
            }

            registrar("Script ejecutado correctamente.");
            return 0;
        }

        // Pre-flight check: probar que se puede conectar SIN ejecutar nada, usado para
        // validar un servidor/instancia ingresado a mano antes de intentar crear la base.
        private static int ProbarConexion(string servidor, Action<string> registrar)
        {
            registrar($"Probando conexión a '{servidor}'...");
            try
            {
                using (var conexion = new SqlConnection(CadenaConexion(servidor)))
                {
                    conexion.Open();
                }
                registrar("Conexión OK.");
                return 0;
            }
            catch (Exception ex)
            {
                registrar("No se pudo conectar: " + ex.Message);
                return 2;
            }
        }

        // Pre-flight de PERMISOS: antes de copiar archivos, confirmar que la cuenta con la
        // que corre el instalador puede crear la base (o actualizarla si ya existe). Sin esto,
        // un usuario sin privilegios recién se enteraba con el script a medio ejecutar.
        //   - Base nueva:     sysadmin o permiso CREATE ANY DATABASE (rol dbcreator).
        //   - Base existente: sysadmin o db_owner de esa base (el script altera el esquema).
        // Códigos: 0 = alcanza, 2 = no se pudo conectar, 3 = permisos insuficientes.
        private static int ChequearPermisos(string servidor, string nombreBD, Action<string> registrar, Action<string> informar)
        {
            registrar($"Chequeando permisos en '{servidor}' para '{nombreBD}'...");
            bool sysadmin, crearBases, existe;
            string login;
            try
            {
                using (var conexion = new SqlConnection(CadenaConexion(servidor)))
                using (var cmd = new SqlCommand(
                    "SELECT ISNULL(IS_SRVROLEMEMBER('sysadmin'), 0), " +
                    "       ISNULL(HAS_PERMS_BY_NAME(NULL, NULL, 'CREATE ANY DATABASE'), 0), " +
                    "       CASE WHEN DB_ID(@bd) IS NULL THEN 0 ELSE 1 END, SUSER_SNAME()", conexion))
                {
                    cmd.Parameters.AddWithValue("@bd", nombreBD);
                    conexion.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        rd.Read();
                        sysadmin = Convert.ToInt32(rd.GetValue(0)) == 1;
                        crearBases = Convert.ToInt32(rd.GetValue(1)) == 1;
                        existe = Convert.ToInt32(rd.GetValue(2)) == 1;
                        login = rd.IsDBNull(3) ? "?" : rd.GetString(3);
                    }
                }
            }
            catch (Exception ex)
            {
                registrar("No se pudo conectar: " + ex.Message);
                informar("No se pudo conectar al servidor: " + ex.Message);
                return 2;
            }

            registrar($"Login: {login} | sysadmin: {sysadmin} | CREATE ANY DATABASE: {crearBases} | la base existe: {existe}");
            if (sysadmin)
            {
                registrar("Permisos OK (sysadmin).");
                return 0;
            }

            if (!existe)
            {
                if (crearBases)
                {
                    registrar("Permisos OK (puede crear bases).");
                    informar("AVISO: el usuario no es sysadmin; dar acceso a los demás usuarios de Windows puede fallar " +
                             "(requiere ALTER ANY LOGIN).");
                    return 0;
                }
                informar($"El usuario de Windows '{login}' no tiene permiso para crear bases de datos en '{servidor}' " +
                         "(hace falta ser sysadmin o miembro de dbcreator).");
                registrar("Permisos INSUFICIENTES para crear la base.");
                return 3;
            }

            bool dbOwner = false;
            try
            {
                var cadena = new SqlConnectionStringBuilder(CadenaConexion(servidor)) { InitialCatalog = nombreBD };
                using (var conexion = new SqlConnection(cadena.ConnectionString))
                using (var cmd = new SqlCommand("SELECT ISNULL(IS_ROLEMEMBER('db_owner'), 0)", conexion))
                {
                    conexion.Open();
                    dbOwner = Convert.ToInt32(cmd.ExecuteScalar()) == 1;
                }
            }
            catch (Exception ex)
            {
                registrar("No se pudo entrar a la base existente: " + ex.Message);
            }

            if (dbOwner)
            {
                registrar("Permisos OK (db_owner de la base existente).");
                return 0;
            }
            informar($"La base {nombreBD} ya existe en '{servidor}' y el usuario de Windows '{login}' no es su dueño " +
                     "(db_owner) ni sysadmin, así que no puede actualizarla.");
            registrar("Permisos INSUFICIENTES para actualizar la base existente.");
            return 3;
        }

        // Backup previo a una ACTUALIZACIÓN: si el script falla a mitad de camino la base
        // queda migrada a medias, y este .bak es lo que permite volver atrás. Ruta relativa
        // → SQL Server lo deja en su carpeta de backups por defecto (la cuenta del servicio
        // siempre puede escribir ahí). El nombre lleva fecha y hora para que reintentar una
        // actualización fallida NO pise el backup bueno con uno de la base ya a medio migrar.
        // COPY_ONLY: no altera la cadena de backups que el cliente pudiera tener armada.
        // Códigos: 0 = OK (la ruta completa va al .out), 4 = la base no existe, 2 = falló.
        private static int RespaldarBaseDeDatos(string servidor, string nombreBD, string version,
                                                Action<string> registrar, Action<string> informar)
        {
            if (!Regex.IsMatch(version, @"^[0-9A-Za-z._-]{1,30}$"))
            {
                registrar($"ERROR: versión inválida '{version}'.");
                return 2;
            }

            string archivo = $"{nombreBD}_pre_{version}_{DateTime.Now:yyyyMMddHHmmss}.bak";
            registrar($"Respaldando '{nombreBD}' en '{servidor}' ({archivo})...");
            try
            {
                using (var conexion = new SqlConnection(CadenaConexion(servidor)))
                {
                    conexion.Open();
                    using (var cmd = new SqlCommand("SELECT DB_ID(@bd)", conexion))
                    {
                        cmd.Parameters.AddWithValue("@bd", nombreBD);
                        if (cmd.ExecuteScalar() is DBNull)
                        {
                            registrar("La base no existe: no hay nada que respaldar.");
                            return 4;
                        }
                    }

                    using (var cmd = new SqlCommand(
                        "BACKUP DATABASE @bd TO DISK = @archivo WITH INIT, COPY_ONLY", conexion))
                    {
                        cmd.Parameters.AddWithValue("@bd", nombreBD);
                        cmd.Parameters.AddWithValue("@archivo", archivo);
                        cmd.CommandTimeout = 0; // una base grande puede tardar
                        cmd.ExecuteNonQuery();
                    }

                    // Ruta completa real (la carpeta la elige SQL Server), para informarla.
                    string ruta = archivo;
                    using (var cmd = new SqlCommand(
                        "SELECT TOP 1 f.physical_device_name FROM msdb.dbo.backupset s " +
                        "JOIN msdb.dbo.backupmediafamily f ON f.media_set_id = s.media_set_id " +
                        "WHERE s.database_name = @bd AND f.physical_device_name LIKE '%' + @archivo " +
                        "ORDER BY s.backup_finish_date DESC", conexion))
                    {
                        cmd.Parameters.AddWithValue("@bd", nombreBD);
                        cmd.Parameters.AddWithValue("@archivo", archivo);
                        object r = cmd.ExecuteScalar();
                        if (r != null && !(r is DBNull)) ruta = (string)r;
                    }

                    registrar("Backup previo a la actualización generado: " + ruta);
                    informar(ruta);
                    return 0;
                }
            }
            catch (Exception ex)
            {
                registrar("No se pudo generar el backup previo: " + ex.Message);
                return 2;
            }
        }

        // Verifica el servicio de Windows del motor SQL y lo arranca si está detenido.
        // Códigos: 0 = corriendo (ya lo estaba o se arrancó ahora), 2 = el servicio no
        // existe, 3 = existe pero no arrancó.
        private static int ChequearYArrancarServicio(string nombreServicio, Action<string> registrar)
        {
            ServiceController servicio;
            try
            {
                servicio = new ServiceController(nombreServicio);
                var status = servicio.Status; // fuerza la lectura; tira si el servicio no existe
                registrar($"Servicio '{nombreServicio}' encontrado. Estado: {status}.");
            }
            catch (Exception)
            {
                registrar($"ERROR: el servicio '{nombreServicio}' no existe en este equipo.");
                return 2;
            }

            if (servicio.Status == ServiceControllerStatus.Running)
            {
                registrar("El servicio ya estaba en ejecución.");
                return 0;
            }

            // Estado transitorio (arrancando, deteniéndose, pausado, etc.): un Start() ahí
            // puede tirar "no puede aceptar comandos de control". Mejor esperar a que se
            // asiente antes de decidir si hace falta arrancarlo.
            if (servicio.Status != ServiceControllerStatus.Stopped)
            {
                registrar($"El servicio está en estado transitorio ({servicio.Status}). Esperando...");
                try
                {
                    servicio.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                    registrar("El servicio quedó en ejecución.");
                    return 0;
                }
                catch (Exception ex)
                {
                    registrar("El servicio no llegó a estar en ejecución: " + ex.Message);
                    return 3;
                }
            }

            registrar("El servicio está detenido. Intentando iniciarlo...");
            try
            {
                servicio.Start();
                servicio.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                registrar("Servicio iniciado correctamente.");
                return 0;
            }
            catch (Exception ex)
            {
                registrar("No se pudo iniciar el servicio (¿faltan permisos de administrador?): " + ex.Message);
                return 3;
            }
        }

        // Usada al desinstalar, solo si el usuario confirma explícitamente que quiere borrar
        // también los datos. nombreBD siempre es la constante fija de la app (no viene de input
        // del usuario), así que la interpolación directa es segura acá.
        private static int BorrarBaseDeDatos(string servidor, string nombreBD, Action<string> registrar)
        {
            registrar($"Conectando a '{servidor}' para eliminar la base '{nombreBD}'...");
            try
            {
                using (var conexion = new SqlConnection(CadenaConexion(servidor)))
                {
                    conexion.Open();
                    string sql =
                        $"IF EXISTS (SELECT 1 FROM sys.databases WHERE name = '{nombreBD}') " +
                        $"BEGIN ALTER DATABASE [{nombreBD}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{nombreBD}]; END";
                    using (var cmd = new SqlCommand(sql, conexion))
                    {
                        cmd.CommandTimeout = 60;
                        cmd.ExecuteNonQuery();
                    }
                }
                registrar("Base de datos eliminada (si existía).");
                return 0;
            }
            catch (Exception ex)
            {
                registrar("No se pudo eliminar la base de datos: " + ex.Message);
                return 2;
            }
        }

        // La app se conecta con Integrated Security, o sea con el usuario de Windows que la
        // abre. El instalador corre elevado y puede hacerlo OTRA cuenta (UAC con credenciales
        // de un administrador distinto): esa cuenta queda con acceso a la base, pero el usuario
        // que después usa la app no tiene login en SQL Server y no puede ni conectarse. Se le
        // da acceso al grupo local "Usuarios" (BUILTIN\Users o BUILTIN\Usuarios según el idioma
        // de Windows, por eso se resuelve por SID y no por nombre) solo sobre esta base. La
        // autorización de cada persona la sigue haciendo el login propio de la app.
        private static int OtorgarAccesoUsuariosLocales(string servidor, string nombreBD, Action<string> registrar)
        {
            string grupo = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null)
                .Translate(typeof(NTAccount)).Value;
            registrar($"Otorgando acceso a '{grupo}' sobre '{nombreBD}' en '{servidor}'...");
            try
            {
                using (var conexion = new SqlConnection(CadenaConexion(servidor)))
                {
                    conexion.Open();
                    const string sql = @"
DECLARE @q nvarchar(max);
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = @grupo)
BEGIN
    SET @q = N'CREATE LOGIN ' + QUOTENAME(@grupo) + N' FROM WINDOWS';
    EXEC (@q);
END
SET @q = N'USE ' + QUOTENAME(@bd) + N';
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N''' + REPLACE(@grupo, '''', '''''') + N''')
    CREATE USER ' + QUOTENAME(@grupo) + N' FOR LOGIN ' + QUOTENAME(@grupo) + N';
ALTER ROLE db_owner ADD MEMBER ' + QUOTENAME(@grupo) + N';';
EXEC (@q);";
                    using (var cmd = new SqlCommand(sql, conexion))
                    {
                        cmd.Parameters.AddWithValue("@grupo", grupo);
                        cmd.Parameters.AddWithValue("@bd", nombreBD);
                        cmd.CommandTimeout = 60;
                        cmd.ExecuteNonQuery();
                    }
                }
                registrar("Acceso otorgado.");
                return 0;
            }
            catch (Exception ex)
            {
                registrar("No se pudo otorgar el acceso: " + ex.Message);
                return 2;
            }
        }

        // Smoke test post-instalación: se conecta como lo hace la app (Initial Catalog = la
        // base) y confirma que quedó lista para el primer login: existe el admin semilla y hay
        // planes de suscripción para operar la comercialización (PN01). Las Experiencias NO se
        // chequean: no se siembran por script (se cargan desde la app). Si falla, el instalador
        // hace rollback en vez de dejar instalada una app en la que no se puede entrar.
        private static int VerificarInstalacion(string servidor, string nombreBD, Action<string> registrar)
        {
            registrar($"Verificando la instalación de '{nombreBD}' en '{servidor}'...");
            try
            {
                var cadena = new SqlConnectionStringBuilder(CadenaConexion(servidor)) { InitialCatalog = nombreBD };
                using (var conexion = new SqlConnection(cadena.ConnectionString))
                {
                    conexion.Open();
                    const string sql =
                        "SELECT (SELECT COUNT(*) FROM Usuario WHERE Username = 'admin'), " +
                        "       (SELECT COUNT(*) FROM PlanSuscripcion)";
                    using (var cmd = new SqlCommand(sql, conexion))
                    using (var rd = cmd.ExecuteReader())
                    {
                        rd.Read();
                        int admins = rd.GetInt32(0), planes = rd.GetInt32(1);
                        registrar($"Usuario admin: {admins} | Planes de suscripción: {planes}");
                        if (admins == 0 || planes == 0)
                        {
                            registrar("ERROR: la base quedó creada pero sin los datos iniciales necesarios.");
                            return 2;
                        }
                    }
                }
                registrar("Verificación OK: la base está lista para el primer login.");
                return 0;
            }
            catch (Exception ex)
            {
                registrar("La verificación falló: " + ex.Message);
                return 2;
            }
        }

        private static string CadenaConexion(string servidor) =>
            $"Data Source={servidor};Integrated Security=True;TrustServerCertificate=True;Connect Timeout=15";

        // Separa el script en batches por líneas que son "GO" (igual que sqlcmd/SSMS). El
        // script del proyecto solo usa "GO" simple, sin "GO N" (repetición), así que no hace
        // falta soportar esa variante.
        private static string[] SplitEnBatches(string script)
        {
            string[] lineas = script.Replace("\r\n", "\n").Split('\n');
            var batches = new List<string>();
            var actual = new StringBuilder();

            foreach (string linea in lineas)
            {
                if (linea.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
                {
                    batches.Add(actual.ToString());
                    actual.Clear();
                }
                else
                {
                    actual.AppendLine(linea);
                }
            }

            if (actual.Length > 0)
                batches.Add(actual.ToString());

            return batches.ToArray();
        }
    }
}
