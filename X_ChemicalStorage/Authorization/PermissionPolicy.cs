namespace X_ChemicalStorage.Authorization
{
    /// <summary>
    /// Protects a controller or action with one or more permission keys.
    /// Several keys = ANY of them is enough:  [HasPermission(Permissions.Items.Create, Permissions.Items.Edit)]
    /// Stack the attribute to require ALL of them.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public sealed class HasPermissionAttribute : AuthorizeAttribute
    {
        public HasPermissionAttribute(params string[] permissions)
        {
            if (permissions == null || permissions.Length == 0)
                throw new ArgumentException("At least one permission is required.", nameof(permissions));
            Permissions = permissions;
            Policy = PermissionPolicy.Build(permissions);
        }

        public string[] Permissions { get; }
    }

    public static class PermissionPolicy
    {
        public const string Prefix = "PERM:";
        public static string Build(params string[] permissions) => Prefix + string.Join('|', permissions);

        public static bool TryParse(string policyName, out string[] permissions)
        {
            if (policyName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            {
                permissions = policyName[Prefix.Length..].Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return permissions.Length > 0;
            }
            // Allow plain keys too, e.g. AuthorizationService.AuthorizeAsync(User, Permissions.Items.View)
            if (PermissionCatalog.IsKnown(policyName))
            {
                permissions = new[] { policyName };
                return true;
            }
            permissions = Array.Empty<string>();
            return false;
        }
    }

    public sealed class PermissionRequirement : IAuthorizationRequirement
    {
        public PermissionRequirement(IReadOnlyList<string> permissions) => Permissions = permissions;
        /// <summary>The user needs ANY of these.</summary>
        public IReadOnlyList<string> Permissions { get; }
    }

    /// <summary>Builds policies on the fly for "PERM:..." names and known permission keys.</summary>
    public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
    {
        private readonly DefaultAuthorizationPolicyProvider _fallback;

        public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
            => _fallback = new DefaultAuthorizationPolicyProvider(options);

        public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();
        public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

        public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        {
            if (PermissionPolicy.TryParse(policyName, out var permissions))
            {
                var policy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddRequirements(new PermissionRequirement(permissions))
                    .Build();
                return Task.FromResult<AuthorizationPolicy?>(policy);
            }
            return _fallback.GetPolicyAsync(policyName);
        }
    }

    public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
    {
        private readonly IPermissionService _permissions;
        public PermissionAuthorizationHandler(IPermissionService permissions) => _permissions = permissions;

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
        {
            if (context.User?.Identity?.IsAuthenticated != true)
                return;

            if (await _permissions.HasAnyAsync(context.User, requirement.Permissions))
                context.Succeed(requirement);
        }
    }

    public static class PermissionHttpExtensions
    {
        /// <summary>Imperative check inside an action: if (!await HttpContext.HasPermissionAsync(Permissions.Items.Edit)) return Forbid();</summary>
        public static Task<bool> HasPermissionAsync(this HttpContext context, params string[] anyOf)
            => context.RequestServices.GetRequiredService<IPermissionService>().HasAnyAsync(context.User, anyOf);
    }
}
