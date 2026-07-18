using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using X_ChemicalStorage.Models;
using X_ChemicalStorage.Models.ViewModels;

namespace X_ChemicalStorage.Controllers
{
    public class RoomsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RoomsController(ApplicationDbContext context)
        {
            _context = context;
        }

        private List<SelectListItem> GetStorageUnitTypesList()
        {
            return _context.StorageUnitTypes
                .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Name })
                .ToList();
        }

        #region List / Tree View
        [HttpGet]
        public IActionResult Index()
        {
            var model = new RoomsViewModel
            {
                RoomsList = _context.Rooms
                    .Include(r => r.StorageUnits)
                        .ThenInclude(u => u.StorageUnitType)
                    .Include(r => r.StorageUnits)
                        .ThenInclude(u => u.Shelves)
                            .ThenInclude(s => s.Boxes)
                    .ToList(),
                ListStorageUnitTypes = GetStorageUnitTypesList()
            };

            return View(model);
        }
        #endregion

        #region Room CRUD
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public IActionResult SaveRoom(Room model)
        {
            if (model.Id == 0)
                _context.Rooms.Add(model);
            else
                _context.Rooms.Update(model);

            _context.SaveChanges();
            return RedirectToAction("Index");
        }

        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public IActionResult DeleteRoom(int id)
        {
            var room = _context.Rooms
                .Include(r => r.StorageUnits)
                .FirstOrDefault(r => r.Id == id);

            if (room == null) return NotFound();

            // منع الحذف لو فيه Lots مخزنة فعليًا جوه الغرفة دي
            bool hasLots = _context.Lots.Any(l => l.Box.Shelf.StorageUnit.RoomId == id);
            if (hasLots)
            {
                TempData["Error"] = "لا يمكن حذف الغرفة لوجود عينات مخزنة بداخلها.";
                return RedirectToAction("Index");
            }

            _context.Rooms.Remove(room);
            _context.SaveChanges();
            return RedirectToAction("Index");
        }
        #endregion

        #region Auto-Generation Wizard
        [HttpGet]
        public IActionResult GenerateWizard()
        {
            var model = new RoomsViewModel
            {
                ListStorageUnitTypes = GetStorageUnitTypesList()
            };
            return View(model);
        }

        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public IActionResult GenerateWizard(GenerateStorageViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.RoomName) || !model.Units.Any())
            {
                TempData["Error"] = "برجاء إدخال اسم الغرفة ونوع واحد على الأقل من وحدات التخزين.";
                return RedirectToAction("GenerateWizard");
            }

            var room = new Room { Name = model.RoomName };
            _context.Rooms.Add(room);
            _context.SaveChanges(); // عشان ناخد الـ Room.Id

            foreach (var unitInput in model.Units)
            {
                for (int u = 1; u <= unitInput.UnitsCount; u++)
                {
                    var typeName = _context.StorageUnitTypes
                        .Where(t => t.Id == unitInput.StorageUnitTypeId)
                        .Select(t => t.Name)
                        .FirstOrDefault() ?? "Unit";

                    var storageUnit = new StorageUnit
                    {
                        RoomId = room.Id,
                        StorageUnitTypeId = unitInput.StorageUnitTypeId,
                        IsHazardous = unitInput.IsHazardous,
                        Name = $"{typeName} - Unit {u}"
                    };
                    _context.StorageUnits.Add(storageUnit);
                    _context.SaveChanges(); // عشان ناخد StorageUnit.Id

                    for (int sh = 1; sh <= unitInput.ShelvesPerUnit; sh++)
                    {
                        var shelf = new Shelf
                        {
                            StorageUnitId = storageUnit.Id,
                            Name = $"Shelf {sh}"
                        };
                        _context.Shelves.Add(shelf);
                        _context.SaveChanges(); // عشان ناخد Shelf.Id

                        for (int b = 1; b <= unitInput.BoxesPerShelf; b++)
                        {
                            _context.Boxes.Add(new Box
                            {
                                ShelfId = shelf.Id,
                                Name = $"Box {b}",
                                RowsCount = unitInput.RowsCount,
                                ColumnsCount = unitInput.ColumnsCount
                            });
                        }
                        _context.SaveChanges();
                    }
                }
            }

            TempData["Success"] = "تم إنشاء هيكل التخزين بنجاح.";
            return RedirectToAction("Index");
        }
        #endregion

        #region Manual Add (بعد التوليد)
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public IActionResult AddStorageUnit(int roomId, int storageUnitTypeId, string name, bool isHazardous)
        {
            _context.StorageUnits.Add(new StorageUnit
            {
                RoomId = roomId,
                StorageUnitTypeId = storageUnitTypeId,
                Name = name,
                IsHazardous = isHazardous
            });
            _context.SaveChanges();
            return RedirectToAction("Index");
        }

        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public IActionResult AddShelf(int storageUnitId, string name)
        {
            _context.Shelves.Add(new Shelf { StorageUnitId = storageUnitId, Name = name });
            _context.SaveChanges();
            return RedirectToAction("Index");
        }

        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public IActionResult AddBox(int shelfId, string name, int rowsCount, int columnsCount)
        {
            _context.Boxes.Add(new Box
            {
                ShelfId = shelfId,
                Name = name,
                RowsCount = rowsCount,
                ColumnsCount = columnsCount
            });
            _context.SaveChanges();
            return RedirectToAction("Index");
        }

        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public IActionResult DeleteStorageUnit(int id)
        {
            if (_context.Lots.Any(l => l.Box.Shelf.StorageUnitId == id))
            {
                TempData["Error"] = "لا يمكن الحذف لوجود عينات مخزنة بداخل هذه الوحدة.";
                return RedirectToAction("Index");
            }
            var unit = _context.StorageUnits.Find(id);
            if (unit != null) { _context.StorageUnits.Remove(unit); _context.SaveChanges(); }
            return RedirectToAction("Index");
        }

        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public IActionResult DeleteShelf(int id)
        {
            if (_context.Lots.Any(l => l.Box.ShelfId == id))
            {
                TempData["Error"] = "لا يمكن الحذف لوجود عينات مخزنة بداخل هذا الرف.";
                return RedirectToAction("Index");
            }
            var shelf = _context.Shelves.Find(id);
            if (shelf != null) { _context.Shelves.Remove(shelf); _context.SaveChanges(); }
            return RedirectToAction("Index");
        }

        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public IActionResult DeleteBox(int id)
        {
            if (_context.Lots.Any(l => l.BoxId == id))
            {
                TempData["Error"] = "لا يمكن الحذف لوجود عينات مخزنة بداخل هذا البوكس.";
                return RedirectToAction("Index");
            }
            var box = _context.Boxes.Find(id);
            if (box != null) { _context.Boxes.Remove(box); _context.SaveChanges(); }
            return RedirectToAction("Index");
        }
        #endregion

        #region Cascading AJAX Endpoints (هتتستخدم في CreateLot لاحقًا)
        [HttpGet]
        public JsonResult GetStorageUnitsByRoom(int roomId)
        {
            var units = _context.StorageUnits
                .Where(u => u.RoomId == roomId)
                .Select(u => new { u.Id, u.Name })
                .ToList();
            return Json(units);
        }

        [HttpGet]
        public JsonResult GetShelvesByStorageUnit(int storageUnitId)
        {
            var shelves = _context.Shelves
                .Where(s => s.StorageUnitId == storageUnitId)
                .Select(s => new { s.Id, s.Name })
                .ToList();
            return Json(shelves);
        }

        [HttpGet]
        public JsonResult GetBoxesByShelf(int shelfId)
        {
            var boxes = _context.Boxes
                .Where(b => b.ShelfId == shelfId)
                .Select(b => new { b.Id, b.Name })
                .ToList();
            return Json(boxes);
        }

        // بيرجع أبعاد البوكس + الأماكن المشغولة عشان نرسم الجدول بالألوان
        [HttpGet]
        public JsonResult GetBoxGrid(int boxId, int? excludeLotId = null)
        {
            var box = _context.Boxes
                .Include(b => b.Lots)
                .FirstOrDefault(b => b.Id == boxId);

            if (box == null) return Json(null);

            var occupied = box.Lots
                .Where(l => l.Id != excludeLotId) // امنع اعتبار مكان اللوت الحالي "مشغول"
                .Select(l => new { l.RowIndex, l.ColumnIndex, l.LotNumber })
                .ToList();

            return Json(new { box.RowsCount, box.ColumnsCount, Occupied = occupied });
        }
        #endregion
    }
}