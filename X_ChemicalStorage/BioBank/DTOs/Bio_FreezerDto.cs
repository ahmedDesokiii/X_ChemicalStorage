// DTOs/FreezerDtos.cs
using X_ChemicalStorage.BioBank.Enums;

namespace X_ChemicalStorage.BioBank.DTOs
{
    public class Bio_CreateFreezerDto
    {
        public string Id { get; set; } = null!;
        public FreezerType Type { get; set; }
        public int Temperature { get; set; }
        public string? Barcode { get; set; }

        public int ShelvesCount { get; set; } = 4;     // ← غيّر هنا
        public int RacksPerShelf { get; set; } = 4;
        public int DrawersPerRack { get; set; } = 5;
        public int BoxesPerDrawer { get; set; } = 5;
        public int TubesPerBox { get; set; } = 100;
    }

    public class Bio_FreezerDto
    {
        public string Id { get; set; } = null!;
        public FreezerType Type { get; set; }
        public string TypeDisplay => Type == FreezerType.Minus80 ? "-80°C" : "-150°C";
        public int Temperature { get; set; }
        public FreezerStatus Status { get; set; }
        public string? Barcode { get; set; }

        public int ShelvesCount { get; set; }           // ← غيّر هنا
        public int RacksPerShelf { get; set; }
        public int DrawersPerRack { get; set; }
        public int BoxesPerDrawer { get; set; }
        public int TubesPerBox { get; set; }

        public long TotalCapacity { get; set; }
        public long OccupiedCount { get; set; }
        public long AvailableCount { get; set; }
        public int OccupancyPercent { get; set; }
    }

    public class Bio_ShelfDto
    {
        public string Id { get; set; } = null!;
        public string FreezerId { get; set; } = null!;
        public int ShelfIndex { get; set; }
        public string DisplayName => $"S{ShelfIndex + 1:D2}";

        public int RacksCount { get; set; }
        public int DrawersCount { get; set; }
        public int BoxesCount { get; set; }
        public long TotalTubes { get; set; }
        public long OccupiedCount { get; set; }
        public int OccupancyPercent { get; set; }
    }

    public class Bio_RackDto
    {
        public string Id { get; set; } = null!;
        public string FreezerId { get; set; } = null!;
        public string? ShelfId { get; set; }
        public int ShelfIndex { get; set; }
        public int RackIndex { get; set; }
        public string DisplayName => $"R{RackIndex + 1:D2}";

        public int DrawersCount { get; set; }
        public int BoxesCount { get; set; }
        public long TotalTubes { get; set; }
        public long OccupiedCount { get; set; }
        public int OccupancyPercent { get; set; }
    }

    public class Bio_DrawerDto
    {
        public string Id { get; set; } = null!;
        public string RackId { get; set; } = null!;
        public int DrawerIndex { get; set; }
        public string DisplayName => $"D{DrawerIndex + 1:D2}";

        public int BoxesCount { get; set; }
        public long TotalTubes { get; set; }
        public long OccupiedCount { get; set; }
        public int OccupancyPercent { get; set; }
    }

    public class Bio_BoxDto
    {
        public string Id { get; set; } = null!;
        public string? DrawerId { get; set; }
        public string? RackId { get; set; }
        public int BoxIndex { get; set; }
        public string DisplayName => $"BX{BoxIndex + 1:D2}";

        public int Rows { get; set; } = 10;
        public int Columns { get; set; } = 10;
        public int TotalPositions => Rows * Columns;
        public int OccupiedCount { get; set; }
        public int ReservedCount { get; set; }
        public int CheckedOutCount { get; set; }   // NEW: slots locked because sample is out for testing
        public int AvailableCount { get; set; }
        public int OccupancyPercent { get; set; }
    }

    public class Bio_TubeDto
    {
        public string Id { get; set; } = null!;
        public string? Barcode { get; set; }   // NEW
        public string BoxId { get; set; } = null!;
        public string Position { get; set; } = null!;   // "A01"
        public int Row { get; set; }
        public int Column { get; set; }
        public TubeStatus Status { get; set; }
        public string? SampleId { get; set; }
        public string? SampleName { get; set; }
    }

    public class Bio_BoxDetailsDto : Bio_BoxDto
    {
        public List<Bio_TubeDto> Bio_Tubes { get; set; } = new();
        public int Temperature { get; set; }
        public string FreezerId { get; set; } = null!;
    }




    public class Bio_UpdateTubeDto
    {
        [Required]
        public TubeStatus Status { get; set; }

        [MaxLength(100)]
        public string? SampleId { get; set; }

        [MaxLength(200)]
        public string? SampleName { get; set; }

        [MaxLength(100)]
        public string? Barcode { get; set; }   // NEW
    }

    public class Bio_OccupyTubeDto
    {
        [Required]
        [MaxLength(5)]
        public string Position { get; set; } = null!;   // "A01", "B12"...

        [Required]
        [MaxLength(100)]
        public string SampleId { get; set; } = null!;

        [MaxLength(200)]
        public string? SampleName { get; set; }

        [MaxLength(100)]
        public string? Barcode { get; set; }   // NEW
    }

    public class Bio_BulkOccupyTubesDto
    {
        [Required]
        public List<Bio_OccupyTubeDto> Tubes { get; set; } = new();
    }

    public class Bio_ClearTubeDto
    {
        [Required]
        [MaxLength(5)]
        public string Position { get; set; } = null!;
    }

    public class Bio_ReserveTubeDto
    {
        [Required]
        [MaxLength(5)]
        public string Position { get; set; } = null!;

        [MaxLength(100)]
        public string? SampleId { get; set; }

        [MaxLength(200)]
        public string? SampleName { get; set; }
    }

}