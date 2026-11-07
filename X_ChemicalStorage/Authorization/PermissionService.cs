using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace X_ChemicalStorage.Authorization
{
    public interface IPermissionService
    {
        /// <summary>True if the user has ANY of the given permissions (Admin role always true).</summary>
        Task<bool> HasAnyAsync(ClaimsPrincipal user, IEnumerable<string> permissions);

        /// <summary>Effective permission keys of the signed-in user (cached).</summary>
        Task<IReadOnlySet<string>> GetPermissionsAsync(ClaimsPrincipal user);

        /// <summary>Effective permission keys of any user, computed from the database (not cached).</summary>
        Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(int userId);

        /// <summary>Clears every cached permission set. Call after any role/user permission change.</summary>
        void Invalidate();
    }

    /// <summary>Singleton "version" token: cancelling it evicts every cached permission set at once.</summary>
    public sealed class PermissionCacheSignal
    {
        private CancellationTokenSource _cts = new();
        public CancellationToken Token => _cts.Token;

        public void Reset()
        {
            var old = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
            old.Cancel();
            old.Dispose();
        }
    }

    public sealed class PermissionService : IPermissionService
    {
        private static readonly ConcurrentDictionary<string, bool> _syncedDatabases = new();
        private static readonly SemaphoreSlim _syncLock = new(1, 1);
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(20);

        private readonly ApplicationDbContext _db;
        private readonly IMemoryCache _cache;
        private readonly PermissionCacheSignal _signal;
        private readonly ILogger<PermissionService> _logger;

        public PermissionService(ApplicationDbContext db, IMemoryCache cache, PermissionCacheSignal signal, ILogger<PermissionService> logger)
        {
            _db = db;
            _cache = cache;
            _signal = signal;
            _logger = logger;
        }

        public static string AdminRole => Roles.Admin.ToString();

        public async Task<bool> HasAnyAsync(ClaimsPrincipal user, IEnumerable<string> permissions)
        {
            if (user?.Identity?.IsAuthenticated != true) return false;
            if (user.IsInRole(AdminRole)) return true;          // Admin can never be locked out

            var granted = await GetPermissionsAsync(user);
            return permissions.Any(granted.Contains);
        }

        public async Task<IReadOnlySet<string>> GetPermissionsAsync(ClaimsPrincipal user)
        {
            if (user?.Identity?.IsAuthenticated != true) return Empty;
            if (user.IsInRole(AdminRole)) return AllKeys();

            if (!int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Empty;

            // Connection is chosen per session (Program.cs), so the cache key includes the database.
            var cacheKey = $"perm:{DatabaseKey}:{userId}";
            if (_cache.TryGetValue(cacheKey, out IReadOnlySet<string>? cached) && cached != null)
                return cached;

            var set = await GetEffectivePermissionsAsync(userId);

            _cache.Set(cacheKey, set, new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(CacheDuration)
                .AddExpirationToken(new CancellationChangeToken(_signal.Token)));
            return set;
        }

        public async Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(int userId)
        {
            await EnsureSyncedAsync();

            var roleIds = _db.UserRoles.Where(ur => ur.UserId == userId).Select(ur => ur.RoleId);

            var isAdmin = await _db.Roles.AnyAsync(r => roleIds.Contains(r.Id) && r.NormalizedName == AdminRole.ToUpperInvariant());
            if (isAdmin) return AllKeys();

            var fromRoles = await _db.RolePermissions
                .Where(rp => roleIds.Contains(rp.RoleId) && rp.Permission!.IsActive)
                .Select(rp => rp.Permission!.Key)
                .ToListAsync();

            var overrides = await _db.UserPermissions
                .Where(up => up.UserId == userId && up.Permission!.IsActive)
                .Select(up => new { up.Permission!.Key, up.IsGranted })
                .ToListAsync();

            var set = new HashSet<string>(fromRoles, StringComparer.OrdinalIgnoreCase);
            foreach (var o in overrides)
            {
                if (o.IsGranted) set.Add(o.Key);
                else set.Remove(o.Key);       // explicit deny wins over the roles
            }
            return set;
        }

        public void Invalidate() => _signal.Reset();

        // ─────────────────────────────── helpers ───────────────────────────────

        private static readonly IReadOnlySet<string> Empty = new HashSet<string>();
        private static IReadOnlySet<string>? _allKeys;
        private static IReadOnlySet<string> AllKeys() =>
            _allKeys ??= new HashSet<string>(PermissionCatalog.All.Select(p => p.Key), StringComparer.OrdinalIgnoreCase);

        private string DatabaseKey
        {
            get
            {
                var conn = _db.Database.GetDbConnection();
                return $"{conn.DataSource}/{conn.Database}";
            }
        }

        /// <summary>Makes sure the catalog is synced into this database once per app run (any selected connection).</summary>
        private async Task EnsureSyncedAsync()
        {
            var key = DatabaseKey;
            if (_syncedDatabases.ContainsKey(key)) return;

            await _syncLock.WaitAsync();
            try
            {
                if (_syncedDatabases.ContainsKey(key)) return;
                await PermissionSeeder.SyncAsync(_db, _logger);
                _syncedDatabases[key] = true;
            }
            finally
            {
                _syncLock.Release();
            }
        }

        internal static void MarkSynced(string databaseKey) => _syncedDatabases[databaseKey] = true;
    }
}
