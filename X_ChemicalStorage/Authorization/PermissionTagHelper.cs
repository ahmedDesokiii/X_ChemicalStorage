using Microsoft.AspNetCore.Razor.TagHelpers;

namespace X_ChemicalStorage.Authorization
{
    /// <summary>
    /// Renders the element only if the user has the permission:
    ///   &lt;li has-permission="@Permissions.Items.View"&gt;...&lt;/li&gt;
    /// Several keys separated by ',' or '|' = ANY of them.
    /// </summary>
    [HtmlTargetElement(Attributes = AttributeName)]
    public sealed class PermissionTagHelper : TagHelper
    {
        public const string AttributeName = "has-permission";
        private readonly IPermissionService _permissions;
        private readonly IHttpContextAccessor _http;

        public PermissionTagHelper(IPermissionService permissions, IHttpContextAccessor http)
        {
            _permissions = permissions;
            _http = http;
        }

        [HtmlAttributeName(AttributeName)]
        public string? Permission { get; set; }

        // Run before other tag helpers so suppressed elements do no extra work.
        public override int Order => -1000;

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            output.Attributes.RemoveAll(AttributeName);
            if (string.IsNullOrWhiteSpace(Permission)) return;

            var keys = Permission.Split(new[] { ',', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var user = _http.HttpContext?.User;
            if (user == null || !await _permissions.HasAnyAsync(user, keys))
                output.SuppressOutput();
        }
    }
}
