using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;
//using static Microsoft.CodeAnalysis.CSharp.SyntaxTokenParser;

namespace ERPWeb_v02.Controllers
{
    [Authorize]
    //[AllowAnonymous]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IPermissionService _permissions;

        public UsersController(UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager, SignInManager<ApplicationUser> signInManager, ApplicationDbContext context, IConfiguration configuration, IPermissionService permissions)
        {
            _permissions = permissions;
            _userManager = userManager;
            _roleManager = roleManager;
            _signInManager = signInManager;
            _context = context;
            _configuration = configuration;
        }
        private void SessionMsg(string MsgType, string Title, string Msg)
        {
            HttpContext.Session.SetString(Helper.MsgType, MsgType);
            HttpContext.Session.SetString(Helper.Title, Title);
            HttpContext.Session.SetString(Helper.Msg, Msg);
        }

        #region All Users
        [HasPermission(Permissions.Users.View)]
        public async Task<IActionResult> Index()
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var users = await _context.Users
                .OrderBy(u => u.CurrentState)
                .ThenBy(u=>u.FullName)
                .ToListAsync();

            var userVM = new List<UserViewModel>();

            foreach (var user in users)
            {
                userVM.Add(new UserViewModel
                {
                    Id = user.Id.ToString(),
                    FullName = user.FullName,
                    PhoneNumber = user.PhoneNumber,
                    UserName = user.UserName,
                    Email = user.Email,
                    CurrentState = user.CurrentState,
                    Roles = await _userManager.GetRolesAsync(user)
                });
            }

            return View(userVM);
        }
        #endregion

        #region User Process [Add & Update]
        [HasPermission(Permissions.Users.Create)]
        [HttpGet]
        public async Task<IActionResult> AddUser(string userId = null)
        {
            var roles = await _roleManager.Roles.Select(r => new CheckBoxViewModel { RoleId = r.Id.ToString(), DisplayValue = r.Name }).ToListAsync();
            userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var addUserVM = new AddUserViewModel
            {
                Roles = roles
            };
            return View(addUserVM);
        }
        [HasPermission(Permissions.Users.Create)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddUser(AddUserViewModel model)
        {

            if (ModelState.IsValid)
                return View(model);

            string[] UserEmail = model.Email.Split('@');
            model.UserName = UserEmail[0];

            if (!model.Roles.Any(r => r.IsSelected))
            {
                SessionMsg(Helper.Warning, "Select Role", "Select at least 1 role !");
                ModelState.AddModelError("Select Role", "Select at least 1 role !");
                //return View(model);
                //return RedirectToAction(nameof(AddUser));
            }

            

            if (await _userManager.FindByNameAsync(model.UserName) != null)
            {
                SessionMsg(Helper.Warning, "Exist UserName", "UserName already exist !");
                ModelState.AddModelError("Exist UserName", "UserName already exist !");
                //return View(model);
                //return RedirectToAction(nameof(AddUser));
            }

            var user = new ApplicationUser()
            {
                Gender = model.Gender,
                PassUser = model.PassUser,
                EmailConfirmed = true,
                CurrentState = 2,
                FullName = model.FullName,
                PhoneNumber = model.PhoneNumber,
                UserName = model.UserName,
                Email = model.Email
            };
            var result = await _userManager.CreateAsync(user, model.Password);
            
            if (result.Succeeded)// Succeeded 
                SessionMsg(Helper.Success, "Add User", "The user has been added successfully !");
            else if (!result.Succeeded)// Not Successeded
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("Roles", error.Description);
                }
                SessionMsg(Helper.Error, "Error Adding User", "An error occurred while adding some data !");

                //return View(model);
            }
           
            await _userManager.AddToRolesAsync(user, model.Roles.Where(r => r.IsSelected).OrderBy(r => r.IsSelected).Select(r => r.DisplayValue));
            //Thread.Sleep(500);
            return RedirectToAction(nameof(Index));
        }

        [HasPermission(Permissions.Users.Edit)]
        public async Task<IActionResult> EditUser(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            string usrId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (user == null)
                return NotFound();

            var profileFormVM = new ProfileFormViewModel
            {
                Id = userId,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                UserName = user.UserName,
                PassUser = user.PassUser,
                Email = user.Email,
                Gender = user.Gender
            };

            return View(profileFormVM);
        }
        
        [HasPermission(Permissions.Users.Edit)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(ProfileFormViewModel model)
        {
            if (ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByIdAsync(model.Id);

            if (user == null)
            {
                SessionMsg(Helper.Error, "Error Editting User", "An error occurred while modifying some data !");
                return NotFound();
            }
            else
            { 
            user.FullName = model.FullName;
            user.PhoneNumber = model.PhoneNumber;
            //user.Email = model.Email;
            user.Gender = model.Gender;

            await _userManager.UpdateAsync(user);
                SessionMsg(Helper.Success, "Edit User", "The user has been modified successfully !");
            }
            //Thread.Sleep(500);
            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region Roles Process [Manage Roles]
        [HasPermission(Permissions.Users.ManageRoles)]
        public async Task<IActionResult> ManageRoles(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
                return NotFound();

            var roles = await _roleManager.Roles.ToListAsync();

            string usrId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var userRolesVM = new UserRolesViewModel
            {
                UserId = user.Id.ToString(),
                UserName = user.UserName,
                Roles = roles.Select(role => new CheckBoxViewModel
                {
                    RoleId = role.Id.ToString(),
                    DisplayValue = role.Name,
                    IsSelected = _userManager.IsInRoleAsync(user, role.Name).Result
                }).ToList()
            };

            return View(userRolesVM);
        }

        [HasPermission(Permissions.Users.ManageRoles)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManageRoles(UserRolesViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.UserId);

            if (user == null)
                return NotFound();

            var userRoles = await _userManager.GetRolesAsync(user);
            var callerIsAdmin = User.IsInRole(PermissionService.AdminRole);

            foreach (var role in model.Roles)
            {
                // Only an Admin may grant or remove the Admin role (prevents privilege escalation)
                if (!callerIsAdmin && role.DisplayValue == PermissionService.AdminRole)
                    continue;

                if (userRoles.Any(r => r == role.DisplayValue) && !role.IsSelected)
                    await _userManager.RemoveFromRoleAsync(user, role.DisplayValue);

                if (!userRoles.Any(r => r == role.DisplayValue) && role.IsSelected)
                    await _userManager.AddToRoleAsync(user, role.DisplayValue);
            }

            var testRole = await _userManager.GetRolesAsync(user);
            if (testRole.Count == 0)
            {
                user.CurrentState = 1;
                user.EmailConfirmed = false;
                await _userManager.UpdateAsync(user);
            }
            else
            {
                user.CurrentState = 2;
                user.EmailConfirmed = true;
                await _userManager.UpdateAsync(user);
            }

            _permissions.Invalidate();
            return RedirectToAction(nameof(Index));
        }

        [HasPermission(Permissions.Users.ManageRoles)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRoles(UserRolesViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.UserId);

            if (user == null)
                return NotFound();

            var userRoles = await _userManager.GetRolesAsync(user);

            var selected = model.Roles.Where(r => r.IsSelected).Select(r => r.DisplayValue).ToList();

            // Only an Admin may grant or remove the Admin role (prevents privilege escalation)
            if (!User.IsInRole(PermissionService.AdminRole))
            {
                selected.Remove(PermissionService.AdminRole);
                if (userRoles.Contains(PermissionService.AdminRole)) selected.Add(PermissionService.AdminRole);
            }

            await _userManager.RemoveFromRolesAsync(user, userRoles);
            await _userManager.AddToRolesAsync(user, selected);

            _permissions.Invalidate();
            return RedirectToAction(nameof(Index));
        }
        #endregion


        #region Delete User
        //public async Task<IActionResult> DeleteUser(string userid)
        //{

        //    var user = await _userManager.FindByIdAsync(userid);

        //    if (user == null)
        //        return NotFound();

        //    user.CurrentState = 0;
        //    user.EmailConfirmed = false;

        //    await _userManager.UpdateAsync(user);

        //    return RedirectToAction(nameof(Index));
        //}
        [HasPermission(Permissions.Users.Delete)]
        public async Task<IActionResult> DeleteUser(string userid)
        {
            if (string.IsNullOrEmpty(userid))
                return BadRequest();

            var user = await _userManager.FindByIdAsync(userid);

            if (user == null)
                return NotFound();

            user.CurrentState = 0;
            user.EmailConfirmed = false;

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return RedirectToAction(nameof(Index));
        }
        #endregion

        #region User-specific Permissions [Grant / Deny overrides]
        [HasPermission(Permissions.Users.ManagePermissions)]
        [HttpGet]
        public async Task<IActionResult> ManagePermissions(int userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            var model = await PermissionMatrix.ForUserAsync(_context, user.Id, roles.Contains(PermissionService.AdminRole));
            model.SubjectId = user.Id;
            model.SubjectName = string.IsNullOrWhiteSpace(user.FullName) ? user.UserName! : user.FullName;
            model.Details = user.UserName;
            model.SubjectRoles = roles.ToList();
            return View(model);
        }

        [HasPermission(Permissions.Users.ManagePermissions)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManagePermissions(int userId, Dictionary<int, string>? overrides)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return NotFound();

            if (await _userManager.IsInRoleAsync(user, PermissionService.AdminRole))
            {
                SessionMsg(Helper.Warning, "صلاحيات المستخدم", "المستخدم ده Admin وعنده كل الصلاحيات، مفيش حاجة تتعدل.");
                return RedirectToAction(nameof(ManagePermissions), new { userId });
            }

            var changes = await PermissionMatrix.SaveUserOverridesAsync(_context, user.Id, overrides ?? new(), User.Identity?.Name);
            _permissions.Invalidate();

            SessionMsg(Helper.Success, "صلاحيات المستخدم", $"تم حفظ الصلاحيات الخاصة ({changes} تعديل).");
            return RedirectToAction(nameof(ManagePermissions), new { userId });
        }
        #endregion
    }
}
