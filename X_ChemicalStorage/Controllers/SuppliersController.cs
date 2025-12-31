using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using X_ChemicalStorage.IRepository;

namespace X_ChemicalStorage.Controllers
{
    public class SuppliersController : Controller
    {
        private readonly IServicesRepository<Supplier> _servicesSupplier ;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public SuppliersController(IServicesRepository<Supplier> servicesSupplier, UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _servicesSupplier = servicesSupplier;
            _userManager = userManager;
            _context = context;
        }

        private void SessionMsg(string MsgType, string Title, string Msg)
        {
            HttpContext.Session.SetString(Helper.MsgType, MsgType);
            HttpContext.Session.SetString(Helper.Title, Title);
            HttpContext.Session.SetString(Helper.Msg, Msg);
        }

        #region List of Suppliers
        public IActionResult Index()
        {
            var suppliers = new SupplierViewModel
            {
                SuppliersList = _servicesSupplier.GetAll(),
                NewSupplier = new Supplier()
            };
            return View(suppliers);
        }
        #endregion

        #region Delete Center [Delete & Delete Log]
        //Delete
        [Authorize(Permissions.Suppliers.Delete_Suppliers)]
        public IActionResult Delete(int Id)
        {
            var userId = _userManager.GetUserId(User);
            if (_servicesSupplier.Delete(Id))
                return RedirectToAction("index", "Suppliers");

            return RedirectToAction("index", "Suppliers");
        }


        ////Delete Log
        //[Authorize(Permissions.Centers.DeleteLog)]
        //public IActionResult DeleteLog(Guid Id)
        //{
        //    if (_servicesCenterLog.DeleteLog(Id))
        //        return RedirectToAction("Centers", "Centers");
        //    return RedirectToAction("Centers", "Centers");
        //}
        #endregion

        #region Add|Edit Center [Create & Update]
        [Authorize(Permissions.Suppliers.Create_Suppliers), Authorize(Permissions.Suppliers.Edit_Suppliers)]
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public IActionResult Save(SupplierViewModel model)
        {
            if (ModelState.IsValid)
            {
                var userId = _userManager.GetUserId(User);

                if (model?.NewSupplier?.Id == 0)
                { //Create
                    //Exist
                    if (_servicesSupplier.FindBy(model.NewSupplier.Name) != null)
                        SessionMsg(Helper.Error, "Exist Supplier !", "This supplier already exists !");

                    else
                    {
                        if (_servicesSupplier.Save(model.NewSupplier))
                            SessionMsg(Helper.Success, "Add Supplier", "The supplier has been added successfully !");
                        else
                            SessionMsg(Helper.Error, "Error Adding Supplier", "An error occurred while adding some data !");
                    }
                }
                else
                { //Update
                    if (_servicesSupplier.Save(model.NewSupplier))
                        SessionMsg(Helper.Success, "Edit Supplier", "The supplier has been modified successfully !");
                    else
                        SessionMsg(Helper.Error, "Error Editting Supplier", "An error occurred while modifying some data !");

                }
            }
            return RedirectToAction("index", "Suppliers");
        }
        #endregion

        #region Details Of Supplier 
        [HttpGet]
        public IActionResult SelectedSupplier(int id)
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            SupplierViewModel model = new()
            {
                NewSupplier = _servicesSupplier.FindBy(id),
                SuppliersList = _servicesSupplier.GetAll()
            };
            return View(model);
        }
        #endregion
        
    }
}
