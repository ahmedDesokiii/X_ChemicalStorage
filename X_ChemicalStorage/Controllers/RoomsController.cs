using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using X_ChemicalStorage.Models;
using X_ChemicalStorage.Models.TempSamples;
using X_ChemicalStorage.Models.ViewModels;

namespace X_ChemicalStorage.Controllers
{
    [Authorize]
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
                .OrderByDescending(s => s.Id)
                .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Name })
                .ToListAsync();
        }

        // NOTE: the "Has…Lots" helpers now mean "has ANY occupant" —
        // a Lot OR a placed temporary sample (BoxId != null). Names kept to avoid touching call sites.

        /// <summary>
        /// Checks if a room contains any stored lots or placed temporary samples
        /// </summary>
        private async Task<bool> RoomHasLotsAsync(int roomId)
        {
            return await _context.Lots
                       .AsNoTracking()
                       .AnyAsync(l => l.Box.Shelf.StorageUnit.RoomId == roomId)
                || await _context.TempSamples
                       .AsNoTracking()
                       .AnyAsync(s => s.BoxId != null && s.Box!.Shelf.StorageUnit.RoomId == roomId);
        }

        /// <summary>
        /// Checks if a storage unit contains any stored lots or placed temporary samples
        /// </summary>
        private async Task<bool> StorageUnitHasLotsAsync(int storageUnitId)
        {
            return await _context.Lots
                       .AsNoTracking()
                       .AnyAsync(l => l.Box.Shelf.StorageUnitId == storageUnitId)
                || await _context.TempSamples
                       .AsNoTracking()
                       .AnyAsync(s => s.BoxId != null && s.Box!.Shelf.StorageUnitId == storageUnitId);
        }

        /// <summary>
        /// Checks if a shelf contains any stored lots or placed temporary samples
        /// </summary>
        private async Task<bool> ShelfHasLotsAsync(int shelfId)
        {
            return await _context.Lots
                       .AsNoTracking()
                       .AnyAsync(l => l.Box.ShelfId == shelfId)
                || await _context.TempSamples
                       .AsNoTracking()
                       .AnyAsync(s => s.BoxId != null && s.Box!.ShelfId == shelfId);
        }

        /// <summary>
        /// Checks if a box contains any stored lots or placed temporary samples
        /// </summary>
        private async Task<bool> BoxHasLotsAsync(int boxId)
        {
            return await _context.Lots
                       .AsNoTracking()
                       .AnyAsync(l => l.BoxId == boxId)
                || await _context.TempSamples
                       .AsNoTracking()
                       .AnyAsync(s => s.BoxId == boxId);
        }

        /// <summary>
        /// Temporary samples per Holding unit (UnitId → count) for the Rooms page meters
        /// </summary>
        private async Task<Dictionary<int, int>> GetHoldingUnitOccupancyAsync()
        {
            return await _context.TempSamples
                .AsNoTracking()
                .Where(s => s.BoxId != null)
                .GroupBy(s => s.Box!.Shelf.StorageUnitId)
                .Select(g => new { UnitId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UnitId, x => x.Count);
        }

        /// <summary>
        /// Temporary samples per box (BoxId → count) for the box badges
        /// </summary>
        private async Task<Dictionary<int, int>> GetHoldingBoxOccupancyAsync()
        {
            return await _context.TempSamples
                .AsNoTracking()
                .Where(s => s.BoxId != null)
                .GroupBy(s => s.BoxId!.Value)
                .Select(g => new { BoxId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.BoxId, x => x.Count);
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
        [HasPermission(Permissions.Rooms.View)]
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
                    .AsSplitQuery()          // avoids the cartesian explosion of Room×Unit×Shelf×Box in one SQL
                    .ToListAsync();

                // Lot occupancy is aggregated in SQL — no Lot rows are loaded
                var lotBoxOcc = await _context.Lots.AsNoTracking()
                    .Where(l => l.BoxId != null)
                    .GroupBy(l => (int)l.BoxId!)
                    .Select(g => new { BoxId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.BoxId, x => x.Count);

                var model = new RoomsViewModel
                {
                    RoomsList = rooms,
                    ListStorageUnitTypes = await GetStorageUnitTypesListAsync(),
                    HoldingOccupancy = await GetHoldingUnitOccupancyAsync(),
                    HoldingBoxOccupancy = await GetHoldingBoxOccupancyAsync(),
                    LotBoxOccupancy = lotBoxOcc
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
        [HasPermission(Permissions.Rooms.Create, Permissions.Rooms.Edit)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveRoom(Room model)
        {
            if (!await HttpContext.HasPermissionAsync(model.Id == 0 ? Permissions.Rooms.Create : Permissions.Rooms.Edit))
                return Forbid();

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

        [HasPermission(Permissions.Rooms.Delete)]
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

                // Prevent deletion if room contains stored lots or temporary samples
                if (await RoomHasLotsAsync(id))
                {
                    TempData["Error"] = "Cannot delete this room because it contains stored lots or temporary samples. Please relocate them first.";
                    return RedirectToAction(nameof(Index));
                }

                // FKs are Restrict → delete the (empty) hierarchy bottom-up in one transaction
                await using (var tx = await _context.Database.BeginTransactionAsync())
                {
                    var unitIds = room.StorageUnits.Select(u => u.Id).ToList();
                    var shelves = await _context.Shelves.Where(sh => unitIds.Contains(sh.StorageUnitId)).ToListAsync();
                    var shelfIds = shelves.Select(sh => sh.Id).ToList();
                    _context.Boxes.RemoveRange(await _context.Boxes.Where(b => shelfIds.Contains(b.ShelfId)).ToListAsync());
                    _context.Shelves.RemoveRange(shelves);
                    _context.StorageUnits.RemoveRange(room.StorageUnits);
                    _context.Rooms.Remove(room);
                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();
                }

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
        [HasPermission(Permissions.Rooms.Generate)]
        [HttpGet]
        public async Task<IActionResult> GenerateWizard()
        {
            var model = new RoomsViewModel
            {
                ListStorageUnitTypes = await GetStorageUnitTypesListAsync()
            };
            return View(model);
        }

        [HasPermission(Permissions.Rooms.Generate)]
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

            if (await _context.Rooms.AnyAsync(r => r.Name.ToLower() == model.RoomName.Trim().ToLower()))
            {
                TempData["Error"] = "A room with this name already exists.";
                return RedirectToAction(nameof(GenerateWizard));
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Build the whole hierarchy in memory, then ONE SaveChanges (was one round-trip per unit/shelf batch)
                var room = new Room { Name = model.RoomName.Trim() };
                var typeNames = await _context.StorageUnitTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name);
                var units = new List<StorageUnit>();
                int totalUnits = 0, totalShelves = 0, totalBoxes = 0;

                foreach (var unitInput in model.Units)
                {
                    var typeName = typeNames.TryGetValue(unitInput.StorageUnitTypeId, out var tn) ? tn : "Unit";
                    var prefix = unitInput.Zone == StorageZone.TemporaryHolding ? "Holding" : typeName;

                    for (int u = 1; u <= unitInput.UnitsCount; u++)
                    {
                        var storageUnit = new StorageUnit
                        {
                            Room = room,
                            StorageUnitTypeId = unitInput.StorageUnitTypeId,
                            IsHazardous = unitInput.IsHazardous,
                            Zone = unitInput.Zone,
                            Name = $"{prefix} - Unit {units.Count(x => x.StorageUnitTypeId == unitInput.StorageUnitTypeId && x.Zone == unitInput.Zone) + 1}",
                            Shelves = new List<Shelf>()
                        };

                        for (int sh = 1; sh <= unitInput.ShelvesPerUnit; sh++)
                        {
                            var shelf = new Shelf { Name = $"Shelf {sh}", Boxes = new List<Box>() };
                            for (int b = 1; b <= unitInput.BoxesPerShelf; b++)
                            {
                                shelf.Boxes.Add(new Box { Name = $"Box {b}", RowsCount = unitInput.RowsCount, ColumnsCount = unitInput.ColumnsCount });
                                totalBoxes++;
                            }
                            storageUnit.Shelves.Add(shelf);
                            totalShelves++;
                        }
                        units.Add(storageUnit);
                        totalUnits++;
                    }
                }

                _context.Rooms.Add(room);
                _context.StorageUnits.AddRange(units);
                await _context.SaveChangesAsync();

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
        [HasPermission(Permissions.Rooms.Create)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddStorageUnit(int roomId, int storageUnitTypeId, string name, bool isHazardous,
                                                        StorageZone zone = StorageZone.Inventory)
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

                if (!Enum.IsDefined(typeof(StorageZone), zone))
                {
                    TempData["Error"] = "Invalid storage zone.";
                    return RedirectToAction(nameof(Index));
                }

                var newName = name.Trim();
                if (await _context.StorageUnits.AnyAsync(u => u.RoomId == roomId && u.Name.ToLower() == newName.ToLower()))
                {
                    TempData["Error"] = "Another storage unit in this room already has this name.";
                    return RedirectToAction(nameof(Index));
                }

                _context.StorageUnits.Add(new StorageUnit
                {
                    RoomId = roomId,
                    StorageUnitTypeId = storageUnitTypeId,
                    Name = newName,
                    IsHazardous = isHazardous,
                    Zone = zone
                });
                await _context.SaveChangesAsync();

                var zoneText = zone == StorageZone.TemporaryHolding ? "Temporary Holding" : "Lots Inventory";
                TempData["Success"] = $"Storage unit '{newName}' has been added to {zoneText}.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding storage unit to room {RoomId}", roomId);
                TempData["Error"] = "An error occurred while adding the storage unit.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HasPermission(Permissions.Rooms.Create)]
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

        [HasPermission(Permissions.Rooms.Create)]
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

        #region Edit Operations
        [HasPermission(Permissions.Rooms.Edit)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditStorageUnit(int id, int storageUnitTypeId, string name, bool isHazardous,
                                                         StorageZone zone = StorageZone.Inventory)
        {
            if (id <= 0 || storageUnitTypeId <= 0 || string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Please provide valid storage unit details.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var unit = await _context.StorageUnits.FindAsync(id);
                if (unit == null)
                {
                    TempData["Error"] = "Storage unit not found.";
                    return RedirectToAction(nameof(Index));
                }

                if (!await _context.StorageUnitTypes.AnyAsync(t => t.Id == storageUnitTypeId))
                {
                    TempData["Error"] = "Selected storage unit type does not exist.";
                    return RedirectToAction(nameof(Index));
                }

                if (!Enum.IsDefined(typeof(StorageZone), zone))
                {
                    TempData["Error"] = "Invalid storage zone.";
                    return RedirectToAction(nameof(Index));
                }

                // Type or zone change is only allowed on an empty unit (computed once)
                bool typeChanged = unit.StorageUnitTypeId != storageUnitTypeId;
                bool zoneChanged = unit.Zone != zone;
                if ((typeChanged || zoneChanged) && await StorageUnitHasLotsAsync(id))
                {
                    TempData["Error"] = zoneChanged
                        ? "Empty the unit before switching it between Lots Inventory and Temporary Holding."
                        : "Cannot change the type of a storage unit that contains stored lots or samples.";
                    return RedirectToAction(nameof(Index));
                }

                var newName = name.Trim();
                if (await _context.StorageUnits.AnyAsync(u =>
                        u.RoomId == unit.RoomId && u.Id != id && u.Name.ToLower() == newName.ToLower()))
                {
                    TempData["Error"] = "Another storage unit in this room already has this name.";
                    return RedirectToAction(nameof(Index));
                }

                unit.Name = newName;
                unit.StorageUnitTypeId = storageUnitTypeId;
                unit.IsHazardous = isHazardous;
                unit.Zone = zone;
                await _context.SaveChangesAsync();

                if (zoneChanged)
                    _logger.LogInformation("Storage unit {UnitId} zone changed to {Zone}", id, zone);

                TempData["Success"] = $"Storage unit '{unit.Name}' has been updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing storage unit {UnitId}", id);
                TempData["Error"] = "An error occurred while updating the storage unit.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HasPermission(Permissions.Rooms.Edit)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditShelf(int id, string name)
        {
            if (id <= 0 || string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Please provide a valid shelf name.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var shelf = await _context.Shelves.FindAsync(id);
                if (shelf == null)
                {
                    TempData["Error"] = "Shelf not found.";
                    return RedirectToAction(nameof(Index));
                }

                var newName = name.Trim();
                if (await _context.Shelves.AnyAsync(s =>
                        s.StorageUnitId == shelf.StorageUnitId && s.Id != id && s.Name.ToLower() == newName.ToLower()))
                {
                    TempData["Error"] = "Another shelf in this unit already has this name.";
                    return RedirectToAction(nameof(Index));
                }

                shelf.Name = newName;
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Shelf '{shelf.Name}' has been updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing shelf {ShelfId}", id);
                TempData["Error"] = "An error occurred while updating the shelf.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HasPermission(Permissions.Rooms.Edit)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditBox(int id, string name, int rowsCount, int columnsCount)
        {
            if (id <= 0 || string.IsNullOrWhiteSpace(name) || rowsCount < 1 || columnsCount < 1)
            {
                TempData["Error"] = "Please provide valid box details.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var box = await _context.Boxes.FindAsync(id);
                if (box == null)
                {
                    TempData["Error"] = "Box not found.";
                    return RedirectToAction(nameof(Index));
                }

                // If the grid is shrinking, make sure no stored lot would fall outside the new grid
                if (rowsCount < box.RowsCount || columnsCount < box.ColumnsCount)
                {
                    bool occupantsOutside =
                        await _context.Lots.AsNoTracking()
                            .AnyAsync(l => l.BoxId == id && (l.RowIndex > rowsCount || l.ColumnIndex > columnsCount))
                     || await _context.TempSamples.AsNoTracking()
                            .AnyAsync(s => s.BoxId == id && (s.RowIndex > rowsCount || s.ColumnIndex > columnsCount));
                    if (occupantsOutside)
                    {
                        TempData["Error"] = "Cannot shrink this box: some stored lots or samples are located outside the new grid size.";
                        return RedirectToAction(nameof(Index));
                    }
                }

                var newName = name.Trim();
                if (await _context.Boxes.AnyAsync(b =>
                        b.ShelfId == box.ShelfId && b.Id != id && b.Name.ToLower() == newName.ToLower()))
                {
                    TempData["Error"] = "Another box on this shelf already has this name.";
                    return RedirectToAction(nameof(Index));
                }

                box.Name = newName;
                box.RowsCount = rowsCount;
                box.ColumnsCount = columnsCount;
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Box '{box.Name}' has been updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing box {BoxId}", id);
                TempData["Error"] = "An error occurred while updating the box.";
                return RedirectToAction(nameof(Index));
            }
        }
        #endregion

        #region Delete Operations
        [HasPermission(Permissions.Rooms.Delete)]
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

                var unit = await _context.StorageUnits
                    .Include(u => u.Shelves).ThenInclude(s => s.Boxes)
                    .FirstOrDefaultAsync(u => u.Id == id);
                if (unit == null)
                {
                    TempData["Error"] = "Storage unit not found.";
                    return RedirectToAction(nameof(Index));
                }

                foreach (var shelf in unit.Shelves)
                    _context.Boxes.RemoveRange(shelf.Boxes);
                _context.Shelves.RemoveRange(unit.Shelves);
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

        [HasPermission(Permissions.Rooms.Delete)]
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

                var shelf = await _context.Shelves
                    .Include(s => s.Boxes)
                    .FirstOrDefaultAsync(s => s.Id == id);
                if (shelf == null)
                {
                    TempData["Error"] = "Shelf not found.";
                    return RedirectToAction(nameof(Index));
                }

                _context.Boxes.RemoveRange(shelf.Boxes);
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

        [HasPermission(Permissions.Rooms.Delete)]
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
        /// <summary>
        /// Units for the Lots cascade (CreateLot). Defaults to Inventory so lots can never
        /// be placed in Temporary Holding units. Temp samples use TempSamples/GetHoldingUnitsByRoom.
        /// </summary>
        [HttpGet]
        public async Task<JsonResult> GetStorageUnitsByRoom(int roomId, StorageZone zone = StorageZone.Inventory)
        {
            var units = await _context.StorageUnits
                .AsNoTracking()
                .Where(u => u.RoomId == roomId && u.Zone == zone)
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
        /// Returns box grid dimensions and ALL occupied slots (lots + temporary samples).
        /// "LotNumber" key is kept for both kinds so the existing CreateLot grid JS works unchanged;
        /// "Kind" lets the Temp Samples grid colour them differently.
        /// </summary>
        [HttpGet]
        public async Task<JsonResult> GetBoxGrid(int boxId, int? excludeLotId = null, int? excludeSampleId = null)
        {
            var box = await _context.Boxes
                .AsNoTracking()
                .Include(b => b.Lots)
                .FirstOrDefaultAsync(b => b.Id == boxId);

            if (box == null) return Json(null);

            var lots = box.Lots
                .Where(l => l.Id != excludeLotId)
                .Select(l => new GridOccupant(Convert.ToInt32(l.RowIndex), Convert.ToInt32(l.ColumnIndex), l.LotNumber, "lot", l.Id))
                .ToList();

            var samples = await _context.TempSamples
                .AsNoTracking()
                .Where(s => s.BoxId == boxId && (excludeSampleId == null || s.Id != excludeSampleId))
                .Select(s => new GridOccupant(s.RowIndex ?? 0, s.ColumnIndex ?? 0, s.SampleCode, "sample", s.Id))
                .ToListAsync();

            var occupied = lots.Concat(samples).ToList();

            return Json(new
            {
                box.RowsCount,
                box.ColumnsCount,
                Occupied = occupied,
                TotalSlots = box.RowsCount * box.ColumnsCount,
                AvailableSlots = (box.RowsCount * box.ColumnsCount) - occupied.Count
            });
        }

        /// <summary>Unified grid occupant (serialised camelCase: rowIndex, columnIndex, lotNumber, kind)</summary>
        private sealed record GridOccupant(int RowIndex, int ColumnIndex, string? LotNumber, string Kind, int Id);
        #endregion
    }
}