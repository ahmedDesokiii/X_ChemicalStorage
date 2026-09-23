// Services/BioBankService.cs
using Microsoft.EntityFrameworkCore;
using X_ChemicalStorage.Data;
using X_ChemicalStorage.BioBank.DTOs;
using X_ChemicalStorage.Models;
using X_ChemicalStorage.BioBank.Enums;

namespace X_ChemicalStorage.BioBank.Services
{
    public class BioBankService : IBioBankService
    {
        private readonly ApplicationDbContext _db;

        public BioBankService(ApplicationDbContext db)
        {
            _db = db;
        }

        // ===================== FREEZERS =====================

        public async Task<List<Bio_FreezerDto>> GetAllFreezersAsync()
        {
            var freezers = await _db.Bio_Freezers
                .AsNoTracking()
                .OrderBy(f => f.Id)
                .ToListAsync();

            var result = new List<Bio_FreezerDto>();

            foreach (var f in freezers)
            {
                result.Add(await MapFreezerToDto(f));
            }

            return result;
        }

        public async Task<Bio_FreezerDto?> GetFreezerByIdAsync(string id)
        {
            var freezer = await _db.Bio_Freezers
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == id);

            return freezer == null ? null : await MapFreezerToDto(freezer);
        }

        public async Task<Bio_FreezerDto> CreateFreezerAsync(Bio_CreateFreezerDto dto)
        {
            // Validation
            if (await _db.Bio_Freezers.AnyAsync(f => f.Id == dto.Id))
                throw new InvalidOperationException($"Freezer with ID '{dto.Id}' already exists.");

            // Apply type-specific defaults
            if (dto.Type == FreezerType.Minus150)
            {
                dto.ShelvesCount = 0;
                dto.DrawersPerRack = 0;
                dto.Temperature = -150;

                // لو المستخدم ماحددش، خليه 12 Box لكل Rack
                if (dto.BoxesPerDrawer <= 0)
                    dto.BoxesPerDrawer = 12;

                if (dto.RacksPerShelf <= 0)
                    dto.RacksPerShelf = 12;
            }
            else
            {
                dto.Temperature = -80;
            }

            var freezer = new Bio_Freezer
            {
                Id = dto.Id.ToUpper(),
                Type = dto.Type,
                Temperature = dto.Temperature,
                Status = FreezerStatus.Online,
                Barcode = dto.Barcode ?? $"{dto.Id}-BC",
                ShelvesCount = dto.ShelvesCount,
                RacksPerShelf = dto.RacksPerShelf,
                DrawersPerRack = dto.DrawersPerRack,
                BoxesPerDrawer = dto.BoxesPerDrawer,
                TubesPerBox = dto.TubesPerBox,
                CreatedAt = DateTime.UtcNow
            };

            _db.Bio_Freezers.Add(freezer);
            await _db.SaveChangesAsync();

            // Generate full hierarchy
            await GenerateHierarchyAsync(freezer);

            return await MapFreezerToDto(freezer);
        }

        public async Task<bool> DeleteFreezerAsync(string id)
        {
            var freezer = await _db.Bio_Freezers.FindAsync(id);
            if (freezer == null) return false;

            _db.Bio_Freezers.Remove(freezer); // Cascade will delete everything
            await _db.SaveChangesAsync();
            return true;
        }

        // ===================== HIERARCHY =====================

        public async Task<List<Bio_ShelfDto>> GetShelvesByFreezerAsync(string freezerId)
        {
            var freezer = await _db.Bio_Freezers.AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == freezerId);

            if (freezer == null || freezer.Type == FreezerType.Minus150)
                return new List<Bio_ShelfDto>();

            var shelves = await _db.Bio_Shelves
                .AsNoTracking()
                .Where(s => s.FreezerId == freezerId)
                .OrderBy(s => s.ShelfIndex)
                .ToListAsync();

            var result = new List<Bio_ShelfDto>();

            foreach (var shelf in shelves)
            {
                var racksCount = freezer.RacksPerShelf;
                var drawersCount = racksCount * freezer.DrawersPerRack;
                var boxesCount = drawersCount * freezer.BoxesPerDrawer;
                var totalTubes = boxesCount * (long)freezer.TubesPerBox;

                var occupied = await CountOccupiedTubesUnderShelf(shelf.Id);

                result.Add(new Bio_ShelfDto
                {
                    Id = shelf.Id,
                    FreezerId = freezerId,
                    ShelfIndex = shelf.ShelfIndex,
                    RacksCount = racksCount,
                    DrawersCount = drawersCount,
                    BoxesCount = boxesCount,
                    TotalTubes = totalTubes,
                    OccupiedCount = occupied,
                    OccupancyPercent = totalTubes == 0 ? 0 : (int)Math.Round(occupied * 100.0 / totalTubes)
                });
            }

            return result;
        }

        public async Task<List<Bio_RackDto>> GetRacksByShelfAsync(string freezerId, int shelfIndex)
        {
            var freezer = await _db.Bio_Freezers.AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == freezerId);

            if (freezer == null) return new List<Bio_RackDto>();

            var racks = await _db.Bio_Racks
                .AsNoTracking()
                .Where(r => r.FreezerId == freezerId && r.ShelfIndex == shelfIndex)
                .OrderBy(r => r.RackIndex)
                .ToListAsync();

            return await MapRacksToDto(racks, freezer);
        }

        public async Task<List<Bio_RackDto>> GetRacksByFreezerAsync(string freezerId)
        {
            var freezer = await _db.Bio_Freezers.AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == freezerId);

            if (freezer == null) return new List<Bio_RackDto>();

            var racks = await _db.Bio_Racks
                .AsNoTracking()
                .Where(r => r.FreezerId == freezerId)
                .OrderBy(r => r.RackIndex)
                .ToListAsync();

            return await MapRacksToDto(racks, freezer);
        }

        public async Task<List<Bio_DrawerDto>> GetDrawersByRackAsync(string rackId)
        {
            var rack = await _db.Bio_Racks.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == rackId);

            if (rack == null) return new List<Bio_DrawerDto>();

            var freezer = await _db.Bio_Freezers.AsNoTracking()
                .FirstAsync(f => f.Id == rack.FreezerId);

            var drawers = await _db.Bio_Drawers
                .AsNoTracking()
                .Where(d => d.RackId == rackId)
                .OrderBy(d => d.DrawerIndex)
                .ToListAsync();

            var result = new List<Bio_DrawerDto>();

            foreach (var d in drawers)
            {
                var boxesCount = freezer.BoxesPerDrawer;
                var totalTubes = boxesCount * (long)freezer.TubesPerBox;
                var occupied = await CountOccupiedTubesUnderDrawer(d.Id);

                result.Add(new Bio_DrawerDto
                {
                    Id = d.Id,
                    RackId = rackId,
                    DrawerIndex = d.DrawerIndex,
                    BoxesCount = boxesCount,
                    TotalTubes = totalTubes,
                    OccupiedCount = occupied,
                    OccupancyPercent = totalTubes == 0 ? 0 : (int)Math.Round(occupied * 100.0 / totalTubes)
                });
            }

            return result;
        }

        public async Task<List<Bio_BoxDto>> GetBoxesByDrawerAsync(string drawerId)
        {
            var boxes = await _db.Bio_Boxes
                .AsNoTracking()
                .Where(b => b.DrawerId == drawerId)
                .OrderBy(b => b.BoxIndex)
                .ToListAsync();

            return await MapBoxesToDto(boxes);
        }

        public async Task<List<Bio_BoxDto>> GetBoxesByRackAsync(string rackId)
        {
            var boxes = await _db.Bio_Boxes
                .AsNoTracking()
                .Where(b => b.RackId == rackId)
                .OrderBy(b => b.BoxIndex)
                .ToListAsync();

            return await MapBoxesToDto(boxes);
        }

        public async Task<Bio_BoxDetailsDto?> GetBoxDetailsAsync(string boxId)
        {
            var box = await _db.Bio_Boxes
                .AsNoTracking()
                .Include(b => b.Bio_Tubes)
                .FirstOrDefaultAsync(b => b.Id == boxId);

            if (box == null) return null;

            // Find freezer temperature
            string freezerId;
            if (!string.IsNullOrEmpty(box.DrawerId))
            {
                var drawer = await _db.Bio_Drawers.AsNoTracking().FirstAsync(d => d.Id == box.DrawerId);
                var rack = await _db.Bio_Racks.AsNoTracking().FirstAsync(r => r.Id == drawer.RackId);
                freezerId = rack.FreezerId;
            }
            else
            {
                var rack = await _db.Bio_Racks.AsNoTracking().FirstAsync(r => r.Id == box.RackId);
                freezerId = rack.FreezerId;
            }

            var freezer = await _db.Bio_Freezers.AsNoTracking().FirstAsync(f => f.Id == freezerId);

            // NEW
            var occupied = box.Bio_Tubes.Count(t => t.Status == TubeStatus.Occupied);
            var reserved = box.Bio_Tubes.Count(t => t.Status == TubeStatus.Reserved);
            var checkedOut = box.Bio_Tubes.Count(t => t.Status == TubeStatus.CheckedOut);
            var total = box.Rows * box.Columns;

            return new Bio_BoxDetailsDto
            {
                Id = box.Id,
                DrawerId = box.DrawerId,
                RackId = box.RackId,
                BoxIndex = box.BoxIndex,
                Rows = box.Rows,
                Columns = box.Columns,
                OccupiedCount = occupied,
                ReservedCount = reserved,
                CheckedOutCount = checkedOut,
                AvailableCount = total - occupied - reserved - checkedOut,
                OccupancyPercent = total == 0 ? 0 : (int)Math.Round((occupied + reserved + checkedOut) * 100.0 / total),
                Temperature = freezer.Temperature,
                FreezerId = freezerId,
                Bio_Tubes = box.Bio_Tubes.Select(t => new Bio_TubeDto
                {
                    Id = t.Id,
                    Barcode = t.Barcode,
                    BoxId = t.BoxId,
                    Position = t.Position,
                    Row = t.Row,
                    Column = t.Column,
                    Status = t.Status,
                    SampleId = t.SampleId,
                    SampleName = t.SampleName
                }).ToList()
            };
        }

        // ===================== SEARCH =====================

        public async Task<List<Bio_FreezerDto>> SearchFreezersAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                return new List<Bio_FreezerDto>();

            query = query.Trim().ToUpper();

            var freezers = await _db.Bio_Freezers
                .AsNoTracking()
                .Where(f => f.Id.Contains(query) || (f.Barcode != null && f.Barcode.Contains(query)))
                .OrderBy(f => f.Id)
                .ToListAsync();

            var result = new List<Bio_FreezerDto>();
            foreach (var f in freezers)
                result.Add(await MapFreezerToDto(f));

            return result;
        }

        public async Task<int> CalculateFreezerOccupancyAsync(string freezerId)
        {
            var freezer = await _db.Bio_Freezers.AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == freezerId);

            if (freezer == null) return 0;

            var total = CalculateTotalCapacity(freezer);
            if (total == 0) return 0;

            var occupied = await CountOccupiedTubesUnderFreezer(freezerId);
            return (int)Math.Round(occupied * 100.0 / total);
        }

        // ===================== PRIVATE HELPERS =====================

        private async Task GenerateHierarchyAsync(Bio_Freezer freezer)
        {
            if (freezer.Type == FreezerType.Minus80)
            {
                for (int s = 0; s < freezer.ShelvesCount; s++)
                {
                    var shelfId = $"{freezer.Id}-S{(s + 1):D2}";
                    var shelf = new Bio_Shelf
                    {
                        Id = shelfId,
                        FreezerId = freezer.Id,
                        ShelfIndex = s
                    };
                    _db.Bio_Shelves.Add(shelf);

                    for (int r = 0; r < freezer.RacksPerShelf; r++)
                    {
                        var rackId = $"{shelfId}-R{(r + 1):D2}";
                        var rack = new Bio_Rack
                        {
                            Id = rackId,
                            FreezerId = freezer.Id,
                            ShelfId = shelfId,
                            ShelfIndex = s,
                            RackIndex = r
                        };
                        _db.Bio_Racks.Add(rack);

                        for (int d = 0; d < freezer.DrawersPerRack; d++)
                        {
                            var drawerId = $"{rackId}-D{(d + 1):D2}";
                            var drawer = new Bio_Drawer
                            {
                                Id = drawerId,
                                RackId = rackId,
                                DrawerIndex = d
                            };
                            _db.Bio_Drawers.Add(drawer);

                            for (int b = 0; b < freezer.BoxesPerDrawer; b++)
                            {
                                var boxId = $"{drawerId}-BX{(b + 1):D2}";
                                _db.Bio_Boxes.Add(new Bio_Box
                                {
                                    Id = boxId,
                                    DrawerId = drawerId,
                                    BoxIndex = b,
                                    Rows = 10,
                                    Columns = 10
                                });
                            }
                        }
                    }
                }
            }
            else // Minus150
            {
                // عدد البوكسات لكل Rack (افتراضي 12)
                int boxesPerRack = freezer.BoxesPerDrawer > 0 ? freezer.BoxesPerDrawer : 12;

                for (int r = 0; r < freezer.RacksPerShelf; r++)
                {
                    var rackId = $"{freezer.Id}-R{(r + 1):D2}";
                    var rack = new Bio_Rack
                    {
                        Id = rackId,
                        FreezerId = freezer.Id,
                        ShelfId = null,
                        ShelfIndex = 0,
                        RackIndex = r
                    };
                    _db.Bio_Racks.Add(rack);

                    // 12 Box (أو القيمة المختارة) تحت كل Rack
                    for (int b = 0; b < boxesPerRack; b++)
                    {
                        var boxId = $"{rackId}-BX{(b + 1):D2}";
                        _db.Bio_Boxes.Add(new Bio_Box
                        {
                            Id = boxId,
                            RackId = rackId,
                            DrawerId = null,
                            BoxIndex = b,
                            Rows = 10,
                            Columns = 10
                        });
                    }
                }
            }

            await _db.SaveChangesAsync();
        }

        private long CalculateTotalCapacity(Bio_Freezer f)
        {
            if (f.Type == FreezerType.Minus80)
                return (long)f.ShelvesCount * f.RacksPerShelf * f.DrawersPerRack * f.BoxesPerDrawer * f.TubesPerBox;

            // -150: racks * boxesPerDrawer * tubes
            return (long)f.RacksPerShelf * f.BoxesPerDrawer * f.TubesPerBox;
        }

        private async Task<Bio_FreezerDto> MapFreezerToDto(Bio_Freezer f)
        {
            var total = CalculateTotalCapacity(f);
            var occupied = await CountOccupiedTubesUnderFreezer(f.Id);

            return new Bio_FreezerDto
            {
                Id = f.Id,
                Type = f.Type,
                Temperature = f.Temperature,
                Status = f.Status,
                Barcode = f.Barcode,
                ShelvesCount = f.ShelvesCount,
                RacksPerShelf = f.RacksPerShelf,
                DrawersPerRack = f.DrawersPerRack,
                BoxesPerDrawer = f.BoxesPerDrawer,
                TubesPerBox = f.TubesPerBox,
                TotalCapacity = total,
                OccupiedCount = occupied,
                AvailableCount = total - occupied,
                OccupancyPercent = total == 0 ? 0 : (int)Math.Round(occupied * 100.0 / total)
            };
        }

        private async Task<List<Bio_RackDto>> MapRacksToDto(List<Bio_Rack> racks, Bio_Freezer freezer)
        {
            var result = new List<Bio_RackDto>();

            foreach (var rack in racks)
            {
                int drawersCount = freezer.Type == FreezerType.Minus150 ? 0 : freezer.DrawersPerRack;
                int boxesCount = freezer.Type == FreezerType.Minus150
                    ? freezer.BoxesPerDrawer
                    : drawersCount * freezer.BoxesPerDrawer;

                long totalTubes = boxesCount * (long)freezer.TubesPerBox;
                long occupied = await CountOccupiedTubesUnderRack(rack.Id);

                result.Add(new Bio_RackDto
                {
                    Id = rack.Id,
                    FreezerId = rack.FreezerId,
                    ShelfId = rack.ShelfId,
                    ShelfIndex = rack.ShelfIndex,
                    RackIndex = rack.RackIndex,
                    DrawersCount = drawersCount,
                    BoxesCount = boxesCount,
                    TotalTubes = totalTubes,
                    OccupiedCount = occupied,
                    OccupancyPercent = totalTubes == 0 ? 0 : (int)Math.Round(occupied * 100.0 / totalTubes)
                });
            }

            return result;
        }

        private async Task<List<Bio_BoxDto>> MapBoxesToDto(List<Bio_Box> boxes)
        {
            var result = new List<Bio_BoxDto>();

            foreach (var box in boxes)
            {
                var stats = await _db.Bio_Tubes
    .AsNoTracking()
    .Where(t => t.BoxId == box.Id)
    .GroupBy(t => 1)
    .Select(g => new
    {
        Occupied = g.Count(t => t.Status == TubeStatus.Occupied),
        Reserved = g.Count(t => t.Status == TubeStatus.Reserved),
        CheckedOut = g.Count(t => t.Status == TubeStatus.CheckedOut)
    })
    .FirstOrDefaultAsync();

                int occupied = stats?.Occupied ?? 0;
                int reserved = stats?.Reserved ?? 0;
                int checkedOut = stats?.CheckedOut ?? 0;
                int total = box.Rows * box.Columns;

                result.Add(new Bio_BoxDto
                {
                    Id = box.Id,
                    DrawerId = box.DrawerId,
                    RackId = box.RackId,
                    BoxIndex = box.BoxIndex,
                    Rows = box.Rows,
                    Columns = box.Columns,
                    OccupiedCount = occupied,
                    ReservedCount = reserved,
                    CheckedOutCount = checkedOut,
                    AvailableCount = total - occupied - reserved - checkedOut,
                    OccupancyPercent = total == 0 ? 0 : (int)Math.Round((occupied + reserved + checkedOut) * 100.0 / total)
                });
            }

            return result;
        }

        // ===== Occupancy Counters =====

        private async Task<long> CountOccupiedTubesUnderFreezer(string freezerId)
        {
            // Count tubes that belong to any box under this freezer
            return await _db.Bio_Tubes
                .AsNoTracking()
                .Where(t => t.Status == TubeStatus.Occupied || t.Status == TubeStatus.Reserved || t.Status == TubeStatus.CheckedOut)
                .Where(t =>
                    _db.Bio_Boxes.Any(b => b.Id == t.BoxId &&
                        (
                            (b.DrawerId != null && _db.Bio_Drawers.Any(d => d.Id == b.DrawerId &&
                                _db.Bio_Racks.Any(r => r.Id == d.RackId && r.FreezerId == freezerId)))
                            ||
                            (b.RackId != null && _db.Bio_Racks.Any(r => r.Id == b.RackId && r.FreezerId == freezerId))
                        )))
                .CountAsync();
        }

        private async Task<long> CountOccupiedTubesUnderShelf(string shelfId)
        {
            return await _db.Bio_Tubes
                .AsNoTracking()
                .Where(t => t.Status == TubeStatus.Occupied || t.Status == TubeStatus.Reserved)
                .Where(t => _db.Bio_Boxes.Any(b => b.Id == t.BoxId &&
                    b.DrawerId != null &&
                    _db.Bio_Drawers.Any(d => d.Id == b.DrawerId &&
                        _db.Bio_Racks.Any(r => r.Id == d.RackId && r.ShelfId == shelfId))))
                .CountAsync();
        }

        private async Task<long> CountOccupiedTubesUnderRack(string rackId)
        {
            return await _db.Bio_Tubes
                .AsNoTracking()
                .Where(t => t.Status == TubeStatus.Occupied || t.Status == TubeStatus.Reserved)
                .Where(t => _db.Bio_Boxes.Any(b => b.Id == t.BoxId &&
                    (b.RackId == rackId ||
                     (b.DrawerId != null && _db.Bio_Drawers.Any(d => d.Id == b.DrawerId && d.RackId == rackId)))))
                .CountAsync();
        }

        private async Task<long> CountOccupiedTubesUnderDrawer(string drawerId)
        {
            return await _db.Bio_Tubes
                .AsNoTracking()
                .Where(t => t.Status == TubeStatus.Occupied || t.Status == TubeStatus.Reserved)
                .Where(t => _db.Bio_Boxes.Any(b => b.Id == t.BoxId && b.DrawerId == drawerId))
                .CountAsync();
        }



        // ===================== TUBE / BOX UPDATES =====================

        public async Task<Bio_TubeDto?> UpdateTubeAsync(string boxId, string position, Bio_UpdateTubeDto dto)
        {
            position = position.ToUpper();

            var tube = await _db.Bio_Tubes
                .FirstOrDefaultAsync(t => t.BoxId == boxId && t.Position == position);

            if (tube == null)
            {
                // Create the tube record if it doesn't exist yet
                var box = await _db.Bio_Boxes.AsNoTracking().FirstOrDefaultAsync(b => b.Id == boxId);
                if (box == null) return null;

                var (row, col) = ParsePosition(position);
                tube = new Bio_Tube

                {
                    Id = $"{boxId}-{position}",
                    BoxId = boxId,
                    Position = position,
                    Row = row,
                    Column = col,
                    Status = dto.Status,
                    SampleId = dto.SampleId,
                    SampleName = dto.SampleName,
                    Barcode = dto.Barcode?.Trim(),
                    StoredAt = dto.Status == TubeStatus.Occupied ? DateTime.UtcNow : null

                };
                _db.Bio_Tubes.Add(tube);
            }
            else
            {
                tube.Status = dto.Status;
                tube.SampleId = dto.SampleId;
                tube.SampleName = dto.SampleName;
                if (!string.IsNullOrWhiteSpace(dto.Barcode))
                    tube.Barcode = dto.Barcode.Trim();
                tube.StoredAt = dto.Status == TubeStatus.Occupied ? DateTime.UtcNow : tube.StoredAt;
            }

            await _db.SaveChangesAsync();
            return MapTubeToDto(tube);
        }

        public async Task<Bio_TubeDto> OccupyTubeAsync(string boxId, Bio_OccupyTubeDto dto)
        {
            var position = dto.Position.ToUpper();

            // Check if position is already occupied
            var existing = await _db.Bio_Tubes
                .FirstOrDefaultAsync(t => t.BoxId == boxId && t.Position == position);

            if (existing != null && (existing.Status == TubeStatus.Occupied || existing.Status == TubeStatus.CheckedOut))
                throw new InvalidOperationException($"Position {position} is already occupied.");

            if (existing == null)
            {
                var box = await _db.Bio_Boxes.AsNoTracking().FirstOrDefaultAsync(b => b.Id == boxId)
                    ?? throw new InvalidOperationException($"Box '{boxId}' not found.");

                var (row, col) = ParsePosition(position);

                existing = new Bio_Tube
                {
                    Id = $"{boxId}-{position}",
                    BoxId = boxId,
                    Position = position,
                    Row = row,
                    Column = col
                };
                _db.Bio_Tubes.Add(existing);
            }

            existing.Status = TubeStatus.Occupied;
            existing.SampleId = dto.SampleId;
            existing.SampleName = dto.SampleName;
            
            existing.StoredAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(dto.Barcode))
                existing.Barcode = dto.Barcode.Trim();

            await _db.SaveChangesAsync();
            return MapTubeToDto(existing);
        }

        public async Task<List<Bio_TubeDto>> BulkOccupyTubesAsync(string boxId, Bio_BulkOccupyTubesDto dto)
        {
            if (dto.Tubes == null || dto.Tubes.Count == 0)
                throw new ArgumentException("No tubes provided for bulk occupy.");

            return await ExecuteInTransactionAsync(async () =>
            {
                var box = await _db.Bio_Boxes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Id == boxId)
                    ?? throw new InvalidOperationException($"Box '{boxId}' not found.");

                var results = new List<Bio_TubeDto>();
                var now = DateTime.UtcNow;

                foreach (var item in dto.Tubes)
                {
                    var position = item.Position.ToUpper().Trim();
                    var (row, col) = ParsePosition(position);

                    var existing = await _db.Bio_Tubes
                        .FirstOrDefaultAsync(t => t.BoxId == boxId && t.Position == position);

                    if (existing != null && (existing.Status == TubeStatus.Occupied || existing.Status == TubeStatus.CheckedOut))
                        continue; // or throw if you want strict behavior

                    if (existing == null)
                    {
                        existing = new Bio_Tube
                        {
                            Id = $"{boxId}-{position}",
                            BoxId = boxId,
                            Position = position,
                            Row = row,
                            Column = col
                        };
                        _db.Bio_Tubes.Add(existing);
                    }

                    existing.Status = TubeStatus.Occupied;
                    existing.SampleId = item.SampleId;
                    existing.SampleName = item.SampleName;
                    existing.StoredAt = now;

                    results.Add(MapTubeToDto(existing));
                }

                return results;
            });
        }

        public async Task<Bio_TubeDto> ReserveTubeAsync(string boxId, Bio_ReserveTubeDto dto)
        {
            var position = dto.Position.ToUpper();

            var existing = await _db.Bio_Tubes
                .FirstOrDefaultAsync(t => t.BoxId == boxId && t.Position == position);

            if (existing != null && (existing.Status == TubeStatus.Occupied || existing.Status == TubeStatus.CheckedOut))
                throw new InvalidOperationException($"Position {position} is already occupied and cannot be reserved.");

            if (existing == null)
            {
                var box = await _db.Bio_Boxes.AsNoTracking().FirstOrDefaultAsync(b => b.Id == boxId)
                    ?? throw new InvalidOperationException($"Box '{boxId}' not found.");

                var (row, col) = ParsePosition(position);

                existing = new Bio_Tube
                {
                    Id = $"{boxId}-{position}",
                    BoxId = boxId,
                    Position = position,
                    Row = row,
                    Column = col
                };
                _db.Bio_Tubes.Add(existing);
            }

            existing.Status = TubeStatus.Reserved;
            existing.SampleId = dto.SampleId;
            existing.SampleName = dto.SampleName;
            existing.StoredAt = null;

            await _db.SaveChangesAsync();
            return MapTubeToDto(existing);
        }

        public async Task<bool> ClearTubeAsync(string boxId, string position)
        {
            position = position.ToUpper();

            var tube = await _db.Bio_Tubes
                .FirstOrDefaultAsync(t => t.BoxId == boxId && t.Position == position);

            if (tube == null) return false;

            // Soft clear – keep the record but mark as Available
            tube.Status = TubeStatus.Available;
            tube.SampleId = null;
            tube.SampleName = null;
            tube.StoredAt = null;

            await _db.SaveChangesAsync();
            return true;
        }

        // ===== Private Helpers =====

        private static (int row, int col) ParsePosition(string position)
        {
            // Expected format: A01, B12, J10 ...
            if (string.IsNullOrWhiteSpace(position) || position.Length < 2)
                throw new ArgumentException("Invalid position format. Expected e.g. A01");

            char rowChar = char.ToUpper(position[0]);
            if (rowChar < 'A' || rowChar > 'J')
                throw new ArgumentException("Row must be A-J");

            if (!int.TryParse(position.Substring(1), out int col) || col < 1 || col > 10)
                throw new ArgumentException("Column must be 01-10");

            return (rowChar - 'A', col - 1);
        }

        private static Bio_TubeDto MapTubeToDto(Bio_Tube t)
        {
            return new Bio_TubeDto
            {
                Id = t.Id,
                Barcode = t.Barcode,
                BoxId = t.BoxId,
                Position = t.Position,
                Row = t.Row,
                Column = t.Column,
                Status = t.Status,
                SampleId = t.SampleId,
                SampleName = t.SampleName
            };
        }


        /// <summary>
        /// Executes an operation inside a database transaction.
        /// Automatically commits on success or rolls back on any exception.
        /// </summary>
        private async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();

            try
            {
                var result = await operation();
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Overload for operations that don't return a value.
        /// </summary>
        private async Task ExecuteInTransactionAsync(Func<Task> operation)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();

            try
            {
                await operation();
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Rebuilds boxes under all racks of a -150°C freezer.
        /// Deletes existing boxes (and their tubes) under each rack, then creates the correct number of boxes.
        /// </summary>
        public async Task<Bio_FreezerDto> RebuildMinus150BoxesAsync(string freezerId, int? boxesPerRack = null)
        {
            var freezer = await _db.Bio_Freezers
                .FirstOrDefaultAsync(f => f.Id == freezerId)
                ?? throw new InvalidOperationException($"Freezer '{freezerId}' not found.");

            if (freezer.Type != FreezerType.Minus150)
                throw new InvalidOperationException($"Freezer '{freezerId}' is not a -150°C freezer.");

            int targetBoxes = boxesPerRack ?? (freezer.BoxesPerDrawer > 0 ? freezer.BoxesPerDrawer : 12);

            // Update config on freezer if needed
            if (freezer.BoxesPerDrawer != targetBoxes)
            {
                freezer.BoxesPerDrawer = targetBoxes;
                freezer.UpdatedAt = DateTime.UtcNow;
            }

            return await ExecuteInTransactionAsync(async () =>
            {
                var racks = await _db.Bio_Racks
                    .Where(r => r.FreezerId == freezerId)
                    .OrderBy(r => r.RackIndex)
                    .ToListAsync();

                if (!racks.Any())
                    throw new InvalidOperationException($"No racks found for freezer '{freezerId}'.");

                foreach (var rack in racks)
                {
                    // 1) Delete existing boxes under this rack (tubes cascade if configured, else delete manually)
                    var existingBoxes = await _db.Bio_Boxes
                        .Where(b => b.RackId == rack.Id)
                        .ToListAsync();

                    if (existingBoxes.Any())
                    {
                        var boxIds = existingBoxes.Select(b => b.Id).ToList();

                        // Delete tubes first (safe even if cascade is Restrict)
                        var tubes = await _db.Bio_Tubes
                            .Where(t => boxIds.Contains(t.BoxId))
                            .ToListAsync();
                        if (tubes.Any())
                            _db.Bio_Tubes.RemoveRange(tubes);

                        _db.Bio_Boxes.RemoveRange(existingBoxes);
                    }

                    // 2) Create new boxes
                    for (int b = 0; b < targetBoxes; b++)
                    {
                        var boxId = $"{rack.Id}-BX{(b + 1):D2}";
                        _db.Bio_Boxes.Add(new Bio_Box
                        {
                            Id = boxId,
                            RackId = rack.Id,
                            DrawerId = null,
                            BoxIndex = b,
                            Rows = 10,
                            Columns = 10
                        });
                    }
                }

                return await MapFreezerToDto(freezer);
            });
        }

    }
}