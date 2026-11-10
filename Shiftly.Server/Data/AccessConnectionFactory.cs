using System;
using System.Data.OleDb;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Shiftly.Server.Data
{
    /// <summary>
    /// Centralized Microsoft Access (.accdb) connection provider for Shiftly.Server.
    /// Configures |DataDirectory|, automatically creates ShiftlyDatabase.accdb if missing,
    /// and resolves Microsoft.ACE.OLEDB.12.0 / 16.0 cleanly.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class AccessConnectionFactory
    {
        private string _resolvedConnectionString;

        public string ConnectionString => _resolvedConnectionString;
        public string DataDirectory { get; }
        public string DatabaseFilePath { get; }

        public AccessConnectionFactory(IConfiguration configuration, IHostEnvironment environment)
        {
            DataDirectory = Path.Combine(environment.ContentRootPath, "App_Data");
            Directory.CreateDirectory(DataDirectory);
            AppDomain.CurrentDomain.SetData("DataDirectory", DataDirectory);

            string configured = configuration.GetConnectionString("DefaultConnection")
                ?? configuration.GetConnectionString("AccessConnection")
                ?? "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=|DataDirectory|\\ShiftlyDatabase.accdb;Persist Security Info=False;";

            _resolvedConnectionString = configured;
            DatabaseFilePath = ResolveDatabasePath(configured, DataDirectory);
        }

        /// <summary>
        /// Ensures ShiftlyDatabase.accdb exists on disk (creating an empty .accdb via ADOX.Catalog if needed)
        /// and opens an OleDbConnection, automatically falling back between ACE 12.0 and ACE 16.0.
        /// </summary>
        public OleDbConnection CreateOpenConnection()
        {
            EnsureDatabaseFileExists();

            try
            {
                var conn = new OleDbConnection(_resolvedConnectionString);
                conn.Open();
                return conn;
            }
            catch (InvalidOperationException ex) when (IsProviderNotRegistered(ex))
            {
                string? alternate = GetAlternateAceProviderConnectionString(_resolvedConnectionString);
                if (alternate != null)
                {
                    var altConn = new OleDbConnection(alternate);
                    altConn.Open();
                    _resolvedConnectionString = alternate;
                    return altConn;
                }

                throw CreateMissingDriverException(ex);
            }
        }

        public void EnsureDatabaseFileExists()
        {
            if (File.Exists(DatabaseFilePath))
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(DatabaseFilePath) ?? DataDirectory);

            Type? catalogType = Type.GetTypeFromProgID("ADOX.Catalog");
            if (catalogType == null)
            {
                throw new InvalidOperationException(
                    $"Cannot create '{DatabaseFilePath}' because ADOX.Catalog is unavailable. " +
                    "Create an empty Microsoft Access database named ShiftlyDatabase.accdb inside App_Data.");
            }

            var builder = new OleDbConnectionStringBuilder(_resolvedConnectionString);
            string primaryProvider = string.IsNullOrWhiteSpace(builder.Provider)
                ? "Microsoft.ACE.OLEDB.12.0"
                : builder.Provider;

            string primaryCreateConnStr = $"Provider={primaryProvider};Data Source={DatabaseFilePath};";

            try
            {
                CreateAccessFileWithAdox(catalogType, primaryCreateConnStr);
            }
            catch (Exception ex)
            {
                string? alternate = GetAlternateAceProviderConnectionString(primaryCreateConnStr);
                if (alternate != null)
                {
                    try
                    {
                        CreateAccessFileWithAdox(catalogType, alternate);
                        _resolvedConnectionString = GetAlternateAceProviderConnectionString(_resolvedConnectionString) ?? _resolvedConnectionString;
                        return;
                    }
                    catch
                    {
                        // Fall through to helpful exception below
                    }
                }

                throw CreateMissingDriverException(ex);
            }
        }

        private static void CreateAccessFileWithAdox(Type catalogType, string concreteConnectionString)
        {
            object? catalog = null;
            try
            {
                catalog = Activator.CreateInstance(catalogType);
                catalogType.InvokeMember(
                    "Create",
                    System.Reflection.BindingFlags.InvokeMethod,
                    null,
                    catalog,
                    new object[] { concreteConnectionString });
            }
            finally
            {
                if (catalog != null)
                {
                    // Close underlying ActiveConnection if opened by ADOX.Catalog.Create
                    try
                    {
                        object? activeConn = catalogType.InvokeMember(
                            "ActiveConnection",
                            System.Reflection.BindingFlags.GetProperty,
                            null,
                            catalog,
                            null);
                        if (activeConn != null)
                        {
                            activeConn.GetType().InvokeMember(
                                "Close",
                                System.Reflection.BindingFlags.InvokeMethod,
                                null,
                                activeConn,
                                null);
                            Marshal.FinalReleaseComObject(activeConn);
                        }
                    }
                    catch { }

                    Marshal.FinalReleaseComObject(catalog);
                }
            }
        }

        private static string ResolveDatabasePath(string connectionString, string dataDirectory)
        {
            var builder = new OleDbConnectionStringBuilder(connectionString);
            string dataSource = builder.DataSource ?? "|DataDirectory|\\ShiftlyDatabase.accdb";
            return dataSource.Replace("|DataDirectory|", dataDirectory, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsProviderNotRegistered(Exception ex) =>
            ex.Message.Contains("not registered", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("Provider cannot be found", StringComparison.OrdinalIgnoreCase);

        private static string? GetAlternateAceProviderConnectionString(string connStr)
        {
            if (connStr.Contains("Microsoft.ACE.OLEDB.12.0", StringComparison.OrdinalIgnoreCase))
                return connStr.Replace("Microsoft.ACE.OLEDB.12.0", "Microsoft.ACE.OLEDB.16.0", StringComparison.OrdinalIgnoreCase);

            if (connStr.Contains("Microsoft.ACE.OLEDB.16.0", StringComparison.OrdinalIgnoreCase))
                return connStr.Replace("Microsoft.ACE.OLEDB.16.0", "Microsoft.ACE.OLEDB.12.0", StringComparison.OrdinalIgnoreCase);

            return null;
        }

        private static InvalidOperationException CreateMissingDriverException(Exception inner) =>
            new InvalidOperationException(
                "Microsoft Access Database Engine (Microsoft.ACE.OLEDB.12.0 / 16.0, 64-bit) is not installed on this machine.\n" +
                "הדרייבר של Microsoft Access אינו מותקן על המחשב. יש להתקין את 'Microsoft Access Database Engine 2016 Redistributable (64-bit)' ולהפעיל מחדש את השרת.",
                inner);
    }
}
