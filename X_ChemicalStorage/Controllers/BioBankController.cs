using Microsoft.AspNetCore.Mvc;

namespace X_ChemicalStorage.Controllers
{
    public class BioBankController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
