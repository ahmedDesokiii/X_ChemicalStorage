namespace ERPWeb_v02.Controllers
{
    [Authorize]
    //[AllowAnonymous]
    public class RolesController : Controller
    {
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly ApplicationDbContext _context;
        private readonly IPermissionService _permissions;

        public RolesController(RoleManager<ApplicationRole> roleManager, ApplicationDbContext context, IPermissionService permissions)
        {
            _roleManager = roleManager;
            _context = context;
            _permissions = permissions;
        }
        private void SessionMsg(string MsgType, string Title, string Msg)
        {
            HttpContext.Session.SetString(Helper.MsgType, MsgType);
            HttpContext.Session.SetString(Helper.Title, Title);
            HttpContext.Session.SetString(Helper.Msg, Msg);
        }

        #region List Of Roles [View]
        [HasPermission(Permissions.Roles.View)]
        public async Task<IActionResult> Index()
        {
            var roles = await _roleManager.Roles.OrderBy(x => x.Name).ToListAsync();
            return View(roles);
        }
        #endregion

        #region Add|Edit Roles [Create & Update]
        [HasPermission(Permissions.Roles.Create, Permissions.Roles.Edit)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> _SaveRole(RoleFormViewModel model)
        {
            if (!await HttpContext.HasPermissionAsync(model.RoleId == null ? Permissions.Roles.Create : Permissions.Roles.Edit))
                return Forbid();

            if (model.Details == null)
                model.Details = "";

            if (model.RoleId == null)
            { //Create

                if (await _roleManager.RoleExistsAsync(model.Name))
                {
                    SessionMsg(Helper.Error, "Exist Role", "Role already exist !");
                    return View("Index", await _roleManager.Roles.OrderBy(x => x.Name).ToListAsync());
                }
                else
                {
                    var role = new ApplicationRole()
                    {
                        Name = model.Name,
                        Details = model.Details

                    };
                    var result = await _roleManager.CreateAsync(role);

                    if (result.Succeeded)// Succeeded 
                        SessionMsg(Helper.Success, "Add Role", "The role has been added successfully !");
                    else // Not Successeded
                        SessionMsg(Helper.Error, "Error Adding Role", "An error occurred while adding some data !");
                }
            }
            else
            { //Update
                var roleUpdate = await _roleManager.FindByIdAsync(model.RoleId);
                if (roleUpdate == null)
                    return NotFound();

                // The Admin role name is what grants full access — never let it be renamed.
                if (roleUpdate.Name == PermissionService.AdminRole && model.Name != PermissionService.AdminRole)
                {
                    SessionMsg(Helper.Error, "Edit Role", "The Admin role cannot be renamed !");
                    return RedirectToAction(nameof(Index));
                }

                roleUpdate.Name = model.Name;
                roleUpdate.Details = model.Details;

                var Result = await _roleManager.UpdateAsync(roleUpdate);

                if (Result.Succeeded) // Succeeded
                    SessionMsg(Helper.Success, "Edit Role", "The role has been modified successfully !");
                else  // Not Successeded
                    SessionMsg(Helper.Error, "Error Editting Role", "An error occurred while modifying some data !");
            }

            return RedirectToAction(nameof(Index), await _roleManager.Roles.OrderBy(x => x.Name).ToListAsync());
        }

        #endregion

        #region Delete Role [Delete]
        [HasPermission(Permissions.Roles.Delete)]
        public async Task<IActionResult> DeleteRole(string Id)
        {
            var role = _roleManager.Roles.FirstOrDefault(x => x.Id.ToString() == Id);
            if (role == null)
                return NotFound();

            if (role.Name == PermissionService.AdminRole)
            {
                SessionMsg(Helper.Error, "Delete Role", "The Admin role cannot be deleted !");
                return RedirectToAction(nameof(Index));
            }

            if ((await _roleManager.DeleteAsync(role)).Succeeded)   // ERP_RolePermissions rows cascade
            {
                _permissions.Invalidate();
                return RedirectToAction(nameof(Index));
            }

            return RedirectToAction("Index");
        }
        #endregion

        #region Manage Permissions
        [HasPermission(Permissions.Roles.ManagePermissions)]
        [HttpGet]
        public async Task<IActionResult> ManagePermissions(int roleId)
        {
            var role = await _roleManager.FindByIdAsync(roleId.ToString());
            if (role == null)
                return NotFound();

            var model = await PermissionMatrix.ForRoleAsync(_context, role.Id, role.Name == PermissionService.AdminRole);
            model.SubjectId = role.Id;
            model.SubjectName = role.Name ?? string.Empty;
            model.Details = role.Details;
            return View(model);
        }

        [HasPermission(Permissions.Roles.ManagePermissions)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManagePermissions(int roleId, List<int>? selected)
        {
            var role = await _roleManager.FindByIdAsync(roleId.ToString());
            if (role == null)
                return NotFound();

            if (role.Name == PermissionService.AdminRole)
            {
                SessionMsg(Helper.Warning, "صلاحيات الدور", "دور الـ Admin عنده كل الصلاحيات دايمًا ومش محتاج تعديل.");
                return RedirectToAction(nameof(ManagePermissions), new { roleId });
            }

            var changes = await PermissionMatrix.SaveRoleAsync(_context, role.Id, selected ?? new(), User.Identity?.Name);
            _permissions.Invalidate();

            SessionMsg(Helper.Success, "صلاحيات الدور", $"تم حفظ صلاحيات الدور {role.Name} ({changes} تعديل).");
            return RedirectToAction(nameof(ManagePermissions), new { roleId });
        }
        #endregion


    }
}
