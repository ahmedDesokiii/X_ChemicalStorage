namespace ERPWeb_v02.Controllers
{
    [Authorize(Roles = "Admin")]
    //[AllowAnonymous]
    public class RolesController : Controller
    {
        private readonly RoleManager<ApplicationRole> _roleManager;

        public RolesController(RoleManager<ApplicationRole> roleManager)
        {
            _roleManager = roleManager;
        }
        private void SessionMsg(string MsgType, string Title, string Msg)
        {
            HttpContext.Session.SetString(Helper.MsgType, MsgType);
            HttpContext.Session.SetString(Helper.Title, Title);
            HttpContext.Session.SetString(Helper.Msg, Msg);
        }

        #region List Of Roles [View]
        public async Task<IActionResult> Index()
        {
            var roles = await _roleManager.Roles.OrderBy(x => x.Name).ToListAsync();
            return View(roles);
        }
        #endregion

        #region Add|Edit Roles [Create & Update]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> _SaveRole(RoleFormViewModel model)
        {
            if (model.Details == null)
                model.Details = "";

            if (model.RoleId == null)
            { //Create

                if (await _roleManager.RoleExistsAsync(model.Name))
                {
                    SessionMsg(Helper.Error, "مجموعة مكررة !", "المجموعة مسجلة من قبل !");
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
                        SessionMsg(Helper.Success, "تم الإضافة !", "تم اضافة مجموعة المستخدم بنجاح!");
                    else // Not Successeded
                        SessionMsg(Helper.Error, "خطأ في الإضافة", "حدث خطأ اثناء اضافة المجموعة!");
                }
            }
            else
            { //Update
                var roleUpdate = await _roleManager.FindByIdAsync(model.RoleId);

                roleUpdate.Name = model.Name;
                roleUpdate.Details = model.Details;

                var Result = await _roleManager.UpdateAsync(roleUpdate);

                if (Result.Succeeded) // Succeeded
                    SessionMsg(Helper.Success, "تم التعديل", "تم تعديل مجموعة المستخدم بنجاح !");
                else  // Not Successeded
                    SessionMsg(Helper.Error, "خطأ في التعديل", "حدث خطأ اثناء تعديل المجموعة !");
            }

            return RedirectToAction(nameof(Index), await _roleManager.Roles.OrderBy(x => x.Name).ToListAsync());
        }

        #endregion

        #region Delete Role [Delete]
        public async Task<IActionResult> DeleteRole(string Id)
        {
            var role = _roleManager.Roles.FirstOrDefault(x => x.Id.ToString() == Id);

            if ((await _roleManager.DeleteAsync(role)).Succeeded)
                return RedirectToAction(nameof(Index));

            return RedirectToAction("Index");
        }
        #endregion

        #region Manage Permissions
        public async Task<IActionResult> ManagePermissions(string roleId)
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = await _roleManager.FindByIdAsync(roleId);

            if (role == null)
                return NotFound();

            var roleClaims = _roleManager.GetClaimsAsync(role).Result.Select(c => c.Value).ToList();
            var allClaims = Permissions.GenerateAllPermissions();
            var allPermissions = allClaims.Select(p => new CheckBoxViewModel { DisplayValue = p }).ToList();

            foreach (var permission in allPermissions)
            {
                if (roleClaims.Any(c => c == permission.DisplayValue))
                    permission.IsSelected = true;
            }
            var permissionVM = new PermissionsFormViewModel()
            {
                RoleId = roleId,
                RoleName = role.Name,
                Details = role.Details,
                RoleCalims = allPermissions
            };

            return View(permissionVM);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManagePermissions(PermissionsFormViewModel model)
        {
            var role = await _roleManager.FindByIdAsync(model.RoleId);

            if (role == null)
                return NotFound();

            var roleClaims = await _roleManager.GetClaimsAsync(role);

            foreach (var claim in roleClaims)
                await _roleManager.RemoveClaimAsync(role, claim);

            var selectedClaims = model.RoleCalims.Where(c => c.IsSelected).ToList();

            foreach (var claim in selectedClaims)
                await _roleManager.AddClaimAsync(role, new Claim("Permission", claim.DisplayValue));

            return RedirectToAction(nameof(Index));
        }
        #endregion


    }
}
