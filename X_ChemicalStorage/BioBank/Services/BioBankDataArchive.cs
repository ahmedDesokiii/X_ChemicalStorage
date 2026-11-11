using System.Data;
using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using X_ChemicalStorage.Data;
using X_ChemicalStorage.Models;

namespace X_ChemicalStorage.BioBank.Services
{
    /// <summary>
    /// The Bio Bank part of the database as one portable .zip:
    ///   manifest.json                     format, time, schema version, row counts
    ///   data/&lt;table&gt;.json              { "columns": [...], "rows": [[...], ...] } for every Bio_* table
    ///   attachments/...                   sample attachments (App_Data\biobank-uploads)
    /// Restoring replaces ONLY those tables (in one transaction) and the attachments folder.
    /// The rest of the application (stock, users, permissions…) is not touched.
    /// </summary>
    public static class BioBankDataArchive
    {
        public const string Format = "x-chemicalstorage/biobank-backup";
        public const int FormatVersion = 1;

        public sealed class Manifest
        {
            public string Format { get; set; } = BioBankDataArchive.Format;
            public int Version { get; set; } = FormatVersion;
            public DateTime CreatedUtc { get; set; }
            public string? Type { get; set; }
            public string? CreatedBy { get; set; }
            public string? Machine { get; set; }
            public string? Database { get; set; }
            public string? SchemaVersion { get; set; }            // last applied EF migration
            public List<TableInfo> Tables { get; set; } = new();
            public int AttachmentFiles { get; set; }
            public Dictionary<string, int> Summary { get; set; } = new();   // freezers, samples, readings…
        }

        public sealed class TableInfo
        {
            public string Name { get; set; } = "";
            public string Schema { get; set; } = "dbo";
            public int Rows { get; set; }
        }

        /// Bio Bank tables from the EF model (every Bio_* entity except the backup log itself).
        public static List<(string Schema, string Table)> Tables(ApplicationDbContext db) =>
            db.Model.GetEntityTypes()
                .Where(t => t.ClrType.Name.StartsWith("Bio_", StringComparison.Ordinal)
                            && t.ClrType != typeof(Bio_BackupLog)
                            && !t.IsOwned()
                            && t.GetTableName() != null)
                .Select(t => (t.GetSchema() ?? "dbo", t.GetTableName()!))
                .Distinct()
                .OrderBy(t => t.Item2, StringComparer.Ordinal)
                .ToList();

        private static string Q(string name) => "[" + name.Replace("]", "]]") + "]";
        private static string Q(string schema, string table) => Q(schema) + "." + Q(table);

        // ================================================================== export
        public static async Task<Manifest> WriteAsync(ApplicationDbContext db, string zipPath, string? attachmentsRoot,
            string type, string createdBy, CancellationToken ct)
        {
            var manifest = new Manifest
            {
                CreatedUtc = DateTime.UtcNow,
                Type = type,
                CreatedBy = createdBy,
                Machine = Environment.MachineName,
                Database = db.Database.GetDbConnection().Database,
                SchemaVersion = (await db.Database.GetAppliedMigrationsAsync(ct)).LastOrDefault()
            };

            var tmp = zipPath + ".part";
            try
            {
                await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    await using var conn = new SqlConnection(db.Database.GetConnectionString());
                    await conn.OpenAsync(ct);

                    foreach (var (schema, table) in Tables(db))
                    {
                        var entry = zip.CreateEntry($"data/{table}.json", CompressionLevel.Optimal);
                        await using var es = entry.Open();
                        var rows = await ExportTableAsync(conn, null, schema, table, es, ct);
                        manifest.Tables.Add(new TableInfo { Name = table, Schema = schema, Rows = rows });
                    }

                    if (!string.IsNullOrWhiteSpace(attachmentsRoot) && Directory.Exists(attachmentsRoot))
                    {
                        foreach (var file in Directory.EnumerateFiles(attachmentsRoot, "*", SearchOption.AllDirectories))
                        {
                            var rel = Path.GetRelativePath(attachmentsRoot, file).Replace('\\', '/');
                            zip.CreateEntryFromFile(file, "attachments/" + rel, CompressionLevel.Fastest);
                            manifest.AttachmentFiles++;
                        }
                    }

                    int Rows(string t) => manifest.Tables.FirstOrDefault(x => x.Name == t)?.Rows ?? 0;
                    manifest.Summary = new Dictionary<string, int>
                    {
                        ["freezers"] = Rows(TableName<Bio_Freezer>(db)),
                        ["samples"] = Rows(TableName<Bio_Sample>(db)),
                        ["readings"] = Rows(TableName<Bio_FreezerReading>(db)),
                        ["alarms"] = Rows(TableName<Bio_FreezerAlarm>(db)),
                        ["attachments"] = manifest.AttachmentFiles
                    };

                    var m = zip.CreateEntry("manifest.json", CompressionLevel.Optimal);
                    await using var ms = m.Open();
                    await JsonSerializer.SerializeAsync(ms, manifest, new JsonSerializerOptions { WriteIndented = true }, ct);
                }
                File.Move(tmp, zipPath, overwrite: true);
                return manifest;
            }
            catch
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                throw;
            }
        }

        private static string TableName<T>(ApplicationDbContext db) => db.Model.FindEntityType(typeof(T))?.GetTableName() ?? typeof(T).Name;

        private static async Task<int> ExportTableAsync(SqlConnection conn, SqlTransaction? tx, string schema, string table, Stream output, CancellationToken ct)
        {
            await using var cmd = new SqlCommand($"SELECT * FROM {Q(schema, table)}", conn, tx) { CommandTimeout = 0 };
            await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
            await using var w = new Utf8JsonWriter(output);

            w.WriteStartObject();
            w.WriteStartArray("columns");
            for (int i = 0; i < r.FieldCount; i++) w.WriteStringValue(r.GetName(i));
            w.WriteEndArray();
            w.WriteStartArray("rows");
            int count = 0;
            while (await r.ReadAsync(ct))
            {
                w.WriteStartArray();
                for (int i = 0; i < r.FieldCount; i++) WriteValue(w, r.IsDBNull(i) ? null : r.GetValue(i));
                w.WriteEndArray();
                if (++count % 500 == 0) await w.FlushAsync(ct);
            }
            w.WriteEndArray();
            w.WriteEndObject();
            await w.FlushAsync(ct);
            return count;
        }

        private static void WriteValue(Utf8JsonWriter w, object? v)
        {
            switch (v)
            {
                case null: w.WriteNullValue(); break;
                case string s: w.WriteStringValue(s); break;
                case bool b: w.WriteBooleanValue(b); break;
                case byte or short or int: w.WriteNumberValue(System.Convert.ToInt32(v)); break;
                case long l: w.WriteNumberValue(l); break;
                case decimal d: w.WriteNumberValue(d); break;
                case double d: w.WriteNumberValue(d); break;
                case float f: w.WriteNumberValue(f); break;
                case DateTime dt: w.WriteStringValue(dt.ToString("o", CultureInfo.InvariantCulture)); break;
                case DateTimeOffset dto: w.WriteStringValue(dto.ToString("o", CultureInfo.InvariantCulture)); break;
                case TimeSpan ts: w.WriteStringValue(ts.ToString("c", CultureInfo.InvariantCulture)); break;
                case Guid g: w.WriteStringValue(g.ToString()); break;
                case byte[] bytes: w.WriteStringValue(Convert.ToBase64String(bytes)); break;
                default: w.WriteStringValue(System.Convert.ToString(v, CultureInfo.InvariantCulture)); break;
            }
        }

        // ================================================================== read / validate
        public static Manifest ReadManifest(string zipPath)
        {
            using var zip = ZipFile.OpenRead(zipPath);
            return ReadManifest(zip);
        }

        private static Manifest ReadManifest(ZipArchive zip)
        {
            var entry = zip.GetEntry("manifest.json") ?? throw new InvalidDataException("This is not a Bio Bank backup (manifest.json is missing).");
            using var s = entry.Open();
            var m = JsonSerializer.Deserialize<Manifest>(s) ?? throw new InvalidDataException("manifest.json is empty.");
            if (m.Format != Format) throw new InvalidDataException("This is not a Bio Bank backup file.");
            if (m.Version > FormatVersion) throw new InvalidDataException($"This backup was made by a newer version of the app (format {m.Version}). Update the app first.");
            foreach (var t in m.Tables)
                if (zip.GetEntry($"data/{t.Name}.json") == null) throw new InvalidDataException($"The backup is incomplete: data for {t.Name} is missing.");
            return m;
        }

        // ================================================================== restore
        /// Replaces every Bio Bank table with the archive's rows in ONE transaction: all or nothing.
        public static async Task<Manifest> RestoreDataAsync(ApplicationDbContext db, string zipPath, CancellationToken ct)
        {
            using var zip = ZipFile.OpenRead(zipPath);
            var manifest = ReadManifest(zip);
            var current = Tables(db);
            var currentNames = current.Select(t => t.Table).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var unknown = manifest.Tables.Where(t => !currentNames.Contains(t.Name)).Select(t => t.Name).ToList();
            if (unknown.Count > 0)
                throw new InvalidDataException("The backup has tables this version of the app does not know: " + string.Join(", ", unknown) + ".");

            await using var conn = new SqlConnection(db.Database.GetConnectionString());
            await conn.OpenAsync(ct);
            await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            // every foreign key that touches a Bio Bank table: off while we swap the rows, re-checked at the end
            var fks = new List<(string Schema, string Table, string Name)>();
            {
                var ids = string.Join(",", current.Select(t => $"OBJECT_ID(N'{Q(t.Schema, t.Table).Replace("'", "''")}')"));
                await using var cmd = new SqlCommand($@"
SELECT OBJECT_SCHEMA_NAME(fk.parent_object_id), OBJECT_NAME(fk.parent_object_id), fk.name
FROM sys.foreign_keys fk
WHERE fk.is_disabled = 0 AND (fk.parent_object_id IN ({ids}) OR fk.referenced_object_id IN ({ids}))", conn, tx);
                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct)) fks.Add((r.GetString(0), r.GetString(1), r.GetString(2)));
            }

            async Task Exec(string sql)
            {
                await using var c = new SqlCommand(sql, conn, tx) { CommandTimeout = 0 };
                await c.ExecuteNonQueryAsync(ct);
            }

            try
            {
                foreach (var fk in fks) await Exec($"ALTER TABLE {Q(fk.Schema, fk.Table)} NOCHECK CONSTRAINT {Q(fk.Name)}");
                foreach (var (schema, table) in current) await Exec($"DELETE FROM {Q(schema, table)}");

                foreach (var t in manifest.Tables.Where(t => t.Rows > 0))
                {
                    var (schema, table) = current.First(c => string.Equals(c.Table, t.Name, StringComparison.OrdinalIgnoreCase));
                    await using var es = zip.GetEntry($"data/{t.Name}.json")!.Open();
                    using var doc = await JsonDocument.ParseAsync(es, cancellationToken: ct);
                    await ImportTableAsync(conn, tx, schema, table, doc.RootElement, ct);
                }

                // re-enable and VERIFY every relation; a broken link (e.g. a user that no longer exists) rolls everything back
                foreach (var fk in fks) await Exec($"ALTER TABLE {Q(fk.Schema, fk.Table)} WITH CHECK CHECK CONSTRAINT {Q(fk.Name)}");

                await tx.CommitAsync(ct);
                return manifest;
            }
            catch (SqlException ex) when (ex.Number == 547)
            {
                await SafeRollback(tx);
                throw new InvalidOperationException("The backup does not fit this database: some Bio Bank records point to data that is not here " +
                    "(for example a user or stock item). Nothing was changed. Details: " + ex.Message);
            }
            catch
            {
                await SafeRollback(tx);
                throw;
            }
        }

        private static async Task SafeRollback(SqlTransaction tx)
        {
            try { await tx.RollbackAsync(); } catch { /* connection already gone */ }
        }

        private static async Task ImportTableAsync(SqlConnection conn, SqlTransaction tx, string schema, string table, JsonElement root, CancellationToken ct)
        {
            var dt = new DataTable();
            await using (var cmd = new SqlCommand($"SELECT TOP 0 * FROM {Q(schema, table)}", conn, tx))
            using (var da = new SqlDataAdapter(cmd))
                da.FillSchema(dt, SchemaType.Source);

            // timestamp / computed columns cannot be written; identity values are kept as they were
            var writable = dt.Columns.Cast<DataColumn>().Where(c => !c.ReadOnly || c.AutoIncrement).ToList();
            foreach (DataColumn c in dt.Columns) { c.ReadOnly = false; c.AutoIncrement = false; c.AllowDBNull = true; }
            dt.Constraints.Clear();
            if (dt.PrimaryKey.Length > 0) dt.PrimaryKey = Array.Empty<DataColumn>();

            var cols = root.GetProperty("columns").EnumerateArray().Select(e => e.GetString() ?? "").ToList();
            var map = cols.Select(n => writable.FirstOrDefault(c => string.Equals(c.ColumnName, n, StringComparison.OrdinalIgnoreCase))).ToList();

            foreach (var row in root.GetProperty("rows").EnumerateArray())
            {
                var dr = dt.NewRow();
                int i = 0;
                foreach (var v in row.EnumerateArray())
                {
                    var col = i < map.Count ? map[i] : null;
                    if (col != null) dr[col] = ToColumnValue(v, col.DataType, table, col.ColumnName);
                    i++;
                }
                dt.Rows.Add(dr);
            }

            using var bulk = new SqlBulkCopy(conn, SqlBulkCopyOptions.KeepIdentity | SqlBulkCopyOptions.KeepNulls, tx)
            {
                DestinationTableName = Q(schema, table),
                BulkCopyTimeout = 0,
                BatchSize = 2000
            };
            // only columns the backup has: a column added in a newer version keeps its database default
            foreach (var c in map.Where(c => c != null).Distinct()) bulk.ColumnMappings.Add(c!.ColumnName, c.ColumnName);
            await bulk.WriteToServerAsync(dt, ct);
        }

        private static object ToColumnValue(JsonElement v, Type type, string table, string column)
        {
            if (v.ValueKind == JsonValueKind.Null) return DBNull.Value;
            try
            {
                if (type == typeof(string)) return v.ValueKind == JsonValueKind.String ? v.GetString()! : v.GetRawText();
                if (type == typeof(bool)) return v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : v.GetRawText() == "1";
                if (type == typeof(DateTime)) return DateTime.Parse(v.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                if (type == typeof(DateTimeOffset)) return DateTimeOffset.Parse(v.GetString()!, CultureInfo.InvariantCulture);
                if (type == typeof(TimeSpan)) return TimeSpan.Parse(v.GetString()!, CultureInfo.InvariantCulture);
                if (type == typeof(Guid)) return Guid.Parse(v.GetString()!);
                if (type == typeof(byte[])) return System.Convert.FromBase64String(v.GetString()!);
                if (type == typeof(int)) return v.GetInt32();
                if (type == typeof(long)) return v.GetInt64();
                if (type == typeof(short)) return v.GetInt16();
                if (type == typeof(byte)) return v.GetByte();
                if (type == typeof(decimal)) return v.GetDecimal();
                if (type == typeof(double)) return v.GetDouble();
                if (type == typeof(float)) return v.GetSingle();
                return System.Convert.ChangeType(v.ValueKind == JsonValueKind.String ? v.GetString()! : v.GetRawText(), type, CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
            {
                throw new InvalidDataException($"Bad value in the backup ({table}.{column}): {v.GetRawText()}");
            }
        }

        // ================================================================== attachments
        /// Swap the attachments folder for the archive's. The old folder is kept next to it until the swap succeeded.
        public static int RestoreAttachments(string zipPath, string attachmentsRoot)
        {
            using var zip = ZipFile.OpenRead(zipPath);
            var entries = zip.Entries.Where(e => e.FullName.StartsWith("attachments/", StringComparison.Ordinal) && !e.FullName.EndsWith("/")).ToList();

            var parent = Path.GetDirectoryName(Path.GetFullPath(attachmentsRoot))!;
            var staging = Path.Combine(parent, "biobank-uploads.restoring");
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
            Directory.CreateDirectory(staging);
            var stagingFull = Path.GetFullPath(staging) + Path.DirectorySeparatorChar;

            foreach (var e in entries)
            {
                var dest = Path.GetFullPath(Path.Combine(staging, e.FullName.Substring("attachments/".Length)));
                if (!dest.StartsWith(stagingFull, StringComparison.OrdinalIgnoreCase)) continue;      // zip-slip guard
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                e.ExtractToFile(dest, overwrite: true);
            }

            var old = Path.Combine(parent, "biobank-uploads.before-restore-" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            if (Directory.Exists(attachmentsRoot)) Directory.Move(attachmentsRoot, old);
            Directory.Move(staging, attachmentsRoot);
            return entries.Count;
        }
    }
}
