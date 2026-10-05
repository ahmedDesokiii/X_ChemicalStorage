using Microsoft.EntityFrameworkCore;
using X_ChemicalStorage.Data;

namespace X_ChemicalStorage.BioBank.Services
{
    public class SqlServerBackupExecutor : IBackupExecutor
    {
        private readonly ApplicationDbContext _db;
        public SqlServerBackupExecutor(ApplicationDbContext db) => _db = db;

        public string DatabaseName => _db.Database.GetDbConnection().Database;
        public string DataSource => _db.Database.GetDbConnection().DataSource ?? "";

        private static string Quote(string name) => "[" + name.Replace("]", "]]") + "]";

        public Task EnsureDirectoryAsync(string directory, CancellationToken ct)
            => _db.Database.ExecuteSqlRawAsync("EXEC master.sys.xp_create_subdir {0}", new object[] { directory }, ct);

        public async Task BackupAsync(string filePath, bool compress, int timeoutSeconds, CancellationToken ct)
        {
            _db.Database.SetCommandTimeout(timeoutSeconds);
            var sql = $"BACKUP DATABASE {Quote(DatabaseName)} TO DISK = {{0}} WITH CHECKSUM, INIT{(compress ? ", COMPRESSION" : "")}, NAME = {{1}}";
            await _db.Database.ExecuteSqlRawAsync(sql, new object[] { filePath, "BioBank full backup " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC" }, ct);
        }

        public async Task VerifyAsync(string filePath, int timeoutSeconds, CancellationToken ct)
        {
            _db.Database.SetCommandTimeout(timeoutSeconds);
            await _db.Database.ExecuteSqlRawAsync("RESTORE VERIFYONLY FROM DISK = {0} WITH CHECKSUM", new object[] { filePath }, ct);
        }
    }
}