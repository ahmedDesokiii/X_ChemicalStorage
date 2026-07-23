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
        private readonly ILogger<RoomsController> _logger;

        public RoomsController(ApplicationDbContext context, ILogger<RoomsController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Helpers
        /// <summary>
        /// Gets the list of storage unit types for dropdowns
        /// </summary>
        private async Task<List<SelectListItem>> GetStorageUnitTypesListAsync()
        {
            return await _context.StorageUnitTypes
                .AsNoTracking()
                .OrderBy(s => s.Name)
                .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Name })
                .ToListAsync();
        }

        /// <summary>
        /// Checks if a room contains any stored lots
        /// </summary>
        private async Task<bool> RoomHasLotsAsync(int roomId)
        {
            return await _context.Lots
                .AsNoTracking()
                .AnyAsync(l => l.Box.Shelf.StorageUnit.RoomId == roomId);
        }

        /// <summary>
        /// Checks if a storage unit contains any stored lots
        /// </summary>
        private async Task<bool> StorageUnitHasLotsAsync(int storageUnitId)
        {
            return await _context.Lots
                .AsNoTracking()
                .AnyAsync(l => l.Box.Shelf.StorageUnitId == storageUnitId);
        }

        /// <summary>
        /// Checks if a shelf contains any stored lots
        /// </summary>
        private async Task<bool> ShelfHasLotsAsync(int shelfId)
        {
            return await _context.Lots
                .AsNoTracking()
                .AnyAsync(l => l.Box.ShelfId == shelfId);
        }

        /// <summary>
        /// Checks if a box contains any stored lots
        /// </summary>
        private async Task<bool> BoxHasLotsAsync(int boxId)
        {
            return await _context.Lots
                .AsNoTracking()
                .AnyAsync(l => l.BoxId == boxId);
        }

        /// <summary>
        /// Gets the name of a storage unit type by ID
        /// </summary>
        private async Task<string> GetStorageUnitTypeNameAsync(int typeId)
        {
            return await _context.StorageUnitTypes
                .AsNoTracking()
                .Where(t => t.Id == typeId)
                .Select(t => t.Name)
                .FirstOrDefaultAsync() ?? "Unit";
        }
        #endregion

        #region List / Tree View
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var rooms = await _context.Rooms
                    .AsNoTracking()
                    .Include(r => r.StorageUnits)
                        .ThenInclude(u => u.StorageUnitType)
                    .Include(r => r.StorageUnits)
                        .ThenInclude(u => u.Shelves)
                            .ThenInclude(s => s.Boxes)
                    .OrderBy(r => r.Name)
                    .ToListAsync();

                var model = new RoomsViewModel
                {
                    RoomsList = rooms,
                    ListStorageUnitTypes = await GetStorageUnitTypesListAsync()
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading rooms index");
                TempData["Error"] = "An error occurred while loading storage locations.";
                return View(new RoomsViewModel { ListStorageUnitTypes = await GetStorageUnitTypesListAsync() });
            }
        }
        #endregion

        #region Room CRUD
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveRoom(Room model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please provide a valid room name.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                if (model.Id == 0)
                {
                    // Create new room
                    if (await _context.Rooms.AnyAsync(r => r.Name.ToLower() == model.Name.ToLower()))
                    {
                        TempData["Error"] = "A room with this name already exists.";
                        return RedirectToAction(nameof(Index));
                    }

                    _context.Rooms.Add(model);
                    _logger.LogInformation("Created new room: {RoomName}", model.Name);
                    TempData["Success"] = $"Room '{model.Name}' has been created successfully.";
                }
                else
                {
                    // Update existing room
                    var existingRoom = await _context.Rooms.FindAsync(model.Id);
                    if (existingRoom == null)
                    {
                        TempData["Error"] = "Room not found.";
                        return RedirectToAction(nameof(Index));
                    }

                    existingRoom.Name = model.Name;
                    _context.Rooms.Update(existingRoom);
                    _logger.LogInformation("Updated room ID {RoomId} to '{RoomName}'", model.Id, model.Name);
                    TempData["Success"] = $"Room '{model.Name}' has been updated successfully.";
                }

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving room");
                TempData["Error"] = "An error occurred while saving the room.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRoom(int id)
        {
            try
            {
                var room = await _context.Rooms
                    .Include(r => r.StorageUnits)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (room == null)
                {
                    TempData["Error"] = "Room not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Prevent deletion if room contains stored lots
                if (await RoomHasLotsAsync(id))
                {
                    TempData["Error"] = "Cannot delete this room because it contains stored samples. Please relocate all samples first.";
                    return RedirectToAction(nameof(Index));
                }

                _context.Rooms.Remove(room);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted room ID {RoomId} - '{RoomName}'", id, room.Name);
                TempData["Success"] = $"Room '{room.Name}' has been deleted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting room ID {RoomId}", id);
                TempData["Error"] = "An error occurred while deleting the room.";
                return RedirectToAction(nameof(Index));
            }
        }
        #endregion

        #region Auto-Generation Wizard
        [HttpGet]
        public async Task<IActionResult> GenerateWizard()
        {
            var model = new RoomsViewModel
            {
                ListStorageUnitTypes = await GetStorageUnitTypesListAsync()
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateWizard(GenerateStorageViewModel model)
        {
            if (!ModelState.IsValid ||
                string.IsNullOrWhiteSpace(model.RoomName) ||
                model.Units == null || !model.Units.Any())
            {
                TempData["Error"] = "Please enter a room name and add at least one storage unit type.";
                return RedirectToAction(nameof(GenerateWizard));
            }

            // Validate all unit configurations
            foreach (var unit in model.Units)
            {
                if (unit.StorageUnitTypeId <= 0 ||
                    unit.UnitsCount < 1 ||
                    unit.ShelvesPerUnit < 1 ||
                    unit.BoxesPerShelf < 1 ||
                    unit.RowsCount < 1 ||
                    unit.ColumnsCount < 1)
                {
                    TempData["Error"] = "Please fill in all fields with valid values.";
                    return RedirectToAction(nameof(GenerateWizard));
                }
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Create the room
                var room = new Room { Name = model.RoomName.Trim() };
                _context.Rooms.Add(room);
                await _context.SaveChangesAsync();

                int totalUnits = 0, totalShelves = 0, totalBoxes = 0;

                foreach (var unitInput in model.Units)
                {
                    var typeName = await GetStorageUnitTypeNameAsync(unitInput.StorageUnitTypeId);

                    for (int u = 1; u <= unitInput.UnitsCount; u++)
                    {
                        var storageUnit = new StorageUnit
                        {
                            RoomId = room.Id,
                            StorageUnitTypeId = unitInput.StorageUnitTypeId,
                            IsHazardous = unitInput.IsHazardous,
                            Name = $"{typeName} - Unit {u}"
                        };
                        _context.StorageUnits.Add(storageUnit);
                        await _context.SaveChangesAsync();
                        totalUnits++;

                        var shelves = new List<Shelf>();
                        for (int sh = 1; sh <= unitInput.ShelvesPerUnit; sh++)
                        {
                            shelves.Add(new Shelf
                            {
                                StorageUnitId = storageUnit.Id,
                                Name = $"Shelf {sh}"
                            });
                        }
                        _context.Shelves.AddRange(shelves);
                        await _context.SaveChangesAsync();
                        totalShelves += shelves.Count;

                        var boxes = new List<Box>();
                        foreach (var shelf in shelves)
                        {
                            for (int b = 1; b <= unitInput.BoxesPerShelf; b++)
                            {
                                boxes.Add(new Box
                                {
                                    ShelfId = shelf.Id,
                                    Name = $"Box {b}",
                                    RowsCount = unitInput.RowsCount,
                                    ColumnsCount = unitInput.ColumnsCount
                                });
                            }
                        }
                        _context.Boxes.AddRange(boxes);
                        await _context.SaveChangesAsync();
                        totalBoxes += boxes.Count;
                    }
                }

                await transaction.CommitAsync();

                var totalCapacity = model.Units.Sum(u =>
                    u.UnitsCount * u.ShelvesPerUnit * u.BoxesPerShelf * u.RowsCount * u.ColumnsCount);

                _logger.LogInformation(
                    "Generated storage structure for room '{RoomName}': {Units} units, {Shelves} shelves, {Boxes} boxes, {Capacity} slots",
                    room.Name, totalUnits, totalShelves, totalBoxes, totalCapacity);

                TempData["Success"] = $"Storage structure generated successfully! Created {totalUnits} units, {totalShelves} shelves, {totalBoxes} boxes with {totalCapacity:N0} total slots.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error generating storage wizard for room '{RoomName}'", model.RoomName);
                TempData["Error"] = "An error occurred while generating the storage structure. Please try again.";
                return RedirectToAction(nameof(GenerateWizard));
            }
        }
        #endregion

        #region Manual Add Operations
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddStorageUnit(int roomId, int storageUnitTypeId, string name, bool isHazardous)
        {
            if (string.IsNullOrWhiteSpace(name) || roomId <= 0 || storageUnitTypeId <= 0)
            {
                TempData["Error"] = "Please provide valid storage unit details.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var room = await _context.Rooms.FindAsync(roomId);
                if (room == null)
                {
                    TempData["Error"] = "Room not found.";
                    return RedirectToAction(nameof(Index));
                }

                _context.StorageUnits.Add(new StorageUnit
                {
                    RoomId = roomId,
                    StorageUnitTypeId = storageUnitTypeId,
                    Name = name.Trim(),
                    IsHazardous = isHazardous
                });
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Storage unit '{name}' has been added successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding storage unit to room {RoomId}", roomId);
                TempData["Error"] = "An error occurred while adding the storage unit.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddShelf(int storageUnitId, string name)
        {
            if (string.IsNullOrWhiteSpace(name) || storageUnitId <= 0)
            {
                TempData["Error"] = "Please provide a valid shelf name.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var unit = await _context.StorageUnits.FindAsync(storageUnitId);
                if (unit == null)
                {
                    TempData["Error"] = "Storage unit not found.";
                    return RedirectToAction(nameof(Index));
                }

                _context.Shelves.Add(new Shelf
                {
                    StorageUnitId = storageUnitId,
                    Name = name.Trim()
                });
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Shelf '{name}' has been added successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding shelf to unit {UnitId}", storageUnitId);
                TempData["Error"] = "An error occurred while adding the shelf.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddBox(int shelfId, string name, int rowsCount, int columnsCount)
        {
            if (string.IsNullOrWhiteSpace(name) || shelfId <= 0 || rowsCount < 1 || columnsCount < 1)
            {
                TempData["Error"] = "Please provide valid box details.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var shelf = await _context.Shelves.FindAsync(shelfId);
                if (shelf == null)
                {
                    TempData["Error"] = "Shelf not found.";
                    return RedirectToAction(nameof(Index));
                }

                _context.Boxes.Add(new Box
                {
                    ShelfId = shelfId,
                    Name = name.Trim(),
                    RowsCount = rowsCount,
                    ColumnsCount = columnsCount
                });
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Box '{name}' has been added successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding box to shelf {ShelfId}", shelfId);
                TempData["Error"] = "An error occurred while adding the box.";
                return RedirectToAction(nameof(Index));
            }
        }
        #endregion

        #region Delete Operations
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStorageUnit(int id)
        {
            try
            {
                if (await StorageUnitHasLotsAsync(id))
                {
                    TempData["Error"] = "Cannot delete this storage unit because it contains stored samples.";
                    return RedirectToAction(nameof(Index));
                }

                var unit = await _context.StorageUnits.FindAsync(id);
                if (unit == null)
                {
                    TempData["Error"] = "Storage unit not found.";
                    return RedirectToAction(nameof(Index));
                }

                _context.StorageUnits.Remove(unit);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Storage unit '{unit.Name}' has been deleted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting storage unit {UnitId}", id);
                TempData["Error"] = "An error occurred while deleting the storage unit.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteShelf(int id)
        {
            try
            {
                if (await ShelfHasLotsAsync(id))
                {
                    TempData["Error"] = "Cannot delete this shelf because it contains stored samples.";
                    return RedirectToAction(nameof(Index));
                }

                var shelf = await _context.Shelves.FindAsync(id);
                if (shelf == null)
                {
                    TempData["Error"] = "Shelf not found.";
                    return RedirectToAction(nameof(Index));
                }

                _context.Shelves.Remove(shelf);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Shelf '{shelf.Name}' has been deleted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting shelf {ShelfId}", id);
                TempData["Error"] = "An error occurred while deleting the shelf.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBox(int id)
        {
            try
            {
                if (await BoxHasLotsAsync(id))
                {
                    TempData["Error"] = "Cannot delete this box because it contains stored samples.";
                    return RedirectToAction(nameof(Index));
                }

                var box = await _context.Boxes.FindAsync(id);
                if (box == null)
                {
                    TempData["Error"] = "Box not found.";
                    return RedirectToAction(nameof(Index));
                }

                _context.Boxes.Remove(box);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Box '{box.Name}' has been deleted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting box {BoxId}", id);
                TempData["Error"] = "An error occurred while deleting the box.";
                return RedirectToAction(nameof(Index));
            }
        }
        #endregion

        #region Cascading AJAX Endpoints
        [HttpGet]
        public async Task<JsonResult> GetStorageUnitsByRoom(int roomId)
        {
            var units = await _context.StorageUnits
                .AsNoTracking()
                .Where(u => u.RoomId == roomId)
                .OrderBy(u => u.Name)
                .Select(u => new { u.Id, u.Name, u.IsHazardous })
                .ToListAsync();
            return Json(units);
        }

        [HttpGet]
        public async Task<JsonResult> GetShelvesByStorageUnit(int storageUnitId)
        {
            var shelves = await _context.Shelves
                .AsNoTracking()
                .Where(s => s.StorageUnitId == storageUnitId)
                .OrderBy(s => s.Name)
                .Select(s => new { s.Id, s.Name })
                .ToListAsync();
            return Json(shelves);
        }

        [HttpGet]
        public async Task<JsonResult> GetBoxesByShelf(int shelfId)
        {
            var boxes = await _context.Boxes
                .AsNoTracking()
                .Where(b => b.ShelfId == shelfId)
                .OrderBy(b => b.Name)
                .Select(b => new { b.Id, b.Name, b.RowsCount, b.ColumnsCount })
                .ToListAsync();
            return Json(boxes);
        }

        /// <summary>
        /// Returns box grid dimensions and occupied slots for visual grid display
        /// </summary>
        [HttpGet]
        public async Task<JsonResult> GetBoxGrid(int boxId, int? excludeLotId = null)
        {
            var box = await _context.Boxes
                .AsNoTracking()
                .Include(b => b.Lots)
                .FirstOrDefaultAsync(b => b.Id == boxId);

            if (box == null) return Json(null);

            var occupied = box.Lots
                .Where(l => l.Id != excludeLotId)
                .Select(l => new { l.RowIndex, l.ColumnIndex, l.LotNumber })
                .ToList();

            return Json(new
            {
                box.RowsCount,
                box.ColumnsCount,
                Occupied = occupied,
                TotalSlots = box.RowsCount * box.ColumnsCount,
                AvailableSlots = (box.RowsCount * box.ColumnsCount) - occupied.Count
            });
        }
        #endregion
    }
}