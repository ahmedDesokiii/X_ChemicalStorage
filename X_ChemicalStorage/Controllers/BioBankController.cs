using Microsoft.AspNetCore.Mvc;
using X_ChemicalStorage.BioBank.DTOs;
using X_ChemicalStorage.BioBank.Enums;
using X_ChemicalStorage.BioBank.Services;

namespace X_ChemicalStorage.Controllers
{
    [Route("biobank")]
    public class BioBankController : Controller
    {
        private readonly IBioBankService _bioBankService;
        private readonly ISampleService _sampleService;

        public BioBankController(IBioBankService bioBankService, ISampleService sampleService)
        {
            _bioBankService = bioBankService;
            _sampleService = sampleService;
        }

        // ==================== VIEW ====================

        [HttpGet]
        [Route("")]
        [Route("index")]
        public IActionResult Index()
        {
            ViewData["Title"] = "Bio Bank Storage";
            return View();
        }
        public IActionResult demo()
        {
            return View();
        }

        // ==================== FREEZERS ====================

        /// <summary>
        /// Get all freezers (Room view)
        /// </summary>
        [HttpGet("api/freezers")]
        public async Task<IActionResult> GetAllFreezers()
        {
            var freezers = await _bioBankService.GetAllFreezersAsync();
            return Ok(freezers);
        }

        /// <summary>
        /// Get single freezer by ID
        /// </summary>
        [HttpGet("api/freezers/{id}")]
        public async Task<IActionResult> GetFreezer(string id)
        {
            var freezer = await _bioBankService.GetFreezerByIdAsync(id);
            if (freezer == null)
                return NotFound(new { message = $"Freezer '{id}' not found." });

            return Ok(freezer);
        }

        /// <summary>
        /// Create a new freezer (and generate full hierarchy)
        /// </summary>
        [HttpPost("api/freezers")]
        public async Task<IActionResult> CreateFreezer([FromBody] Bio_CreateFreezerDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var created = await _bioBankService.CreateFreezerAsync(dto);
                return CreatedAtAction(nameof(GetFreezer), new { id = created.Id }, created);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Delete a freezer (cascade deletes entire hierarchy)
        /// </summary>
        [HttpDelete("api/freezers/{id}")]
        public async Task<IActionResult> DeleteFreezer(string id)
        {
            var deleted = await _bioBankService.DeleteFreezerAsync(id);
            if (!deleted)
                return NotFound(new { message = $"Freezer '{id}' not found." });

            return NoContent();
        }

        // ==================== HIERARCHY ====================

        /// <summary>
        /// Get shelves of a -80°C freezer
        /// </summary>
        [HttpGet("api/freezers/{freezerId}/shelves")]
        public async Task<IActionResult> GetShelves(string freezerId)
        {
            var shelves = await _bioBankService.GetShelvesByFreezerAsync(freezerId);
            return Ok(shelves);
        }

        /// <summary>
        /// Get racks under a specific shelf (-80°C)
        /// </summary>
        [HttpGet("api/freezers/{freezerId}/shelves/{shelfIndex:int}/racks")]
        public async Task<IActionResult> GetRacksByShelf(string freezerId, int shelfIndex)
        {
            var racks = await _bioBankService.GetRacksByShelfAsync(freezerId, shelfIndex);
            return Ok(racks);
        }

        /// <summary>
        /// Get racks of a -150°C freezer (no shelves)
        /// </summary>
        [HttpGet("api/freezers/{freezerId}/racks")]
        public async Task<IActionResult> GetRacksByFreezer(string freezerId)
        {
            var racks = await _bioBankService.GetRacksByFreezerAsync(freezerId);
            return Ok(racks);
        }

        /// <summary>
        /// Get drawers under a rack
        /// </summary>
        [HttpGet("api/racks/{rackId}/drawers")]
        public async Task<IActionResult> GetDrawers(string rackId)
        {
            var drawers = await _bioBankService.GetDrawersByRackAsync(rackId);
            return Ok(drawers);
        }

        /// <summary>
        /// Get boxes under a drawer (-80°C)
        /// </summary>
        [HttpGet("api/drawers/{drawerId}/boxes")]
        public async Task<IActionResult> GetBoxesByDrawer(string drawerId)
        {
            var boxes = await _bioBankService.GetBoxesByDrawerAsync(drawerId);
            return Ok(boxes);
        }

        /// <summary>
        /// Get boxes under a rack (-150°C)
        /// </summary>
        [HttpGet("api/racks/{rackId}/boxes")]
        public async Task<IActionResult> GetBoxesByRack(string rackId)
        {
            var boxes = await _bioBankService.GetBoxesByRackAsync(rackId);
            return Ok(boxes);
        }

        /// <summary>
        /// Get full box details + all tubes (10×10 grid)
        /// </summary>
        [HttpGet("api/boxes/{boxId}")]
        public async Task<IActionResult> GetBoxDetails(string boxId)
        {
            var box = await _bioBankService.GetBoxDetailsAsync(boxId);
            if (box == null)
                return NotFound(new { message = $"Box '{boxId}' not found." });

            return Ok(box);
        }

        // ==================== SEARCH ====================

        /// <summary>
        /// Search freezers by ID or barcode
        /// </summary>
        [HttpGet("api/search")]
        public async Task<IActionResult> Search([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                return Ok(new List<Bio_FreezerDto>());

            var results = await _bioBankService.SearchFreezersAsync(q);
            return Ok(results);
        }

        // ==================== OCCUPANCY ====================

        /// <summary>
        /// Get calculated occupancy % for a freezer
        /// </summary>
        [HttpGet("api/freezers/{id}/occupancy")]
        public async Task<IActionResult> GetOccupancy(string id)
        {
            var percent = await _bioBankService.CalculateFreezerOccupancyAsync(id);
            return Ok(new { freezerId = id, occupancyPercent = percent });
        }

        // ==================== BOX / TUBE UPDATES ====================

        /// <summary>
        /// Update a single tube (status + sample info)
        /// </summary>
        [HttpPut("api/boxes/{boxId}/tubes/{position}")]
        public async Task<IActionResult> UpdateTube(string boxId, string position, [FromBody] Bio_UpdateTubeDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var tube = await _bioBankService.UpdateTubeAsync(boxId, position, dto);
                if (tube == null)
                    return NotFound(new { message = $"Box '{boxId}' or position '{position}' not found." });

                return Ok(tube);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Occupy a tube position with a sample
        /// </summary>
        [HttpPost("api/boxes/{boxId}/occupy")]
        public async Task<IActionResult> OccupyTube(string boxId, [FromBody] Bio_OccupyTubeDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var tube = await _bioBankService.OccupyTubeAsync(boxId, dto);
                return Ok(tube);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Occupy multiple tube positions at once
        /// </summary>
        [HttpPost("api/boxes/{boxId}/occupy-bulk")]
        public async Task<IActionResult> BulkOccupyTubes(string boxId, [FromBody] Bio_BulkOccupyTubesDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var tubes = await _bioBankService.BulkOccupyTubesAsync(boxId, dto);
                return Ok(tubes);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Reserve a tube position
        /// </summary>
        [HttpPost("api/boxes/{boxId}/reserve")]
        public async Task<IActionResult> ReserveTube(string boxId, [FromBody] Bio_ReserveTubeDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var tube = await _bioBankService.ReserveTubeAsync(boxId, dto);
                return Ok(tube);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Clear / free a tube position (withdrawal)
        /// </summary>
        [HttpDelete("api/boxes/{boxId}/tubes/{position}")]
        public async Task<IActionResult> ClearTube(string boxId, string position)
        {
            var cleared = await _bioBankService.ClearTubeAsync(boxId, position);
            if (!cleared)
                return NotFound(new { message = $"Tube at position '{position}' in box '{boxId}' not found." });

            return NoContent();
        }

        
        // ========== SAMPLES ==========

        [HttpPost("api/samples/store")]
        public async Task<IActionResult> StoreSample([FromBody] StoreSampleDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var result = await _sampleService.StoreAsync(dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPost("api/samples/reserve")]
        public async Task<IActionResult> ReserveSample([FromBody] ReserveSampleDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var result = await _sampleService.ReserveAsync(dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPost("api/samples/withdraw")]
        public async Task<IActionResult> WithdrawSample([FromBody] WithdrawSampleDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var result = await _sampleService.WithdrawAsync(dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        }

        [HttpPost("api/samples/transfer")]
        public async Task<IActionResult> TransferSample([FromBody] TransferSampleDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var result = await _sampleService.TransferAsync(dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpDelete("api/boxes/{boxId}/tubes/{position}/reserve")]
        public async Task<IActionResult> CancelReserve(string boxId, string position, [FromQuery] string? performedBy = null)
        {
            var ok = await _sampleService.CancelReserveAsync(boxId, position, performedBy);
            if (!ok) return NotFound(new { message = "No active reservation found." });
            return NoContent();
        }

        [HttpGet("api/samples/{id}")]
        public async Task<IActionResult> GetSample(string id)
        {
            var s = await _sampleService.GetByIdAsync(id);
            if (s == null) return NotFound(new { message = "Sample not found." });
            return Ok(s);
        }

        [HttpGet("api/samples/{id}/history")]
        public async Task<IActionResult> GetSampleHistory(string id)
        {
            var history = await _sampleService.GetHistoryAsync(id);
            return Ok(history);
        }

        [HttpGet("api/samples/search")]
        public async Task<IActionResult> SearchSamples([FromQuery] string q)
        {
            var results = await _sampleService.SearchAsync(q ?? "");
            return Ok(results);
        }

        [HttpGet("api/boxes/{boxId}/tubes/{position}")]
        public async Task<IActionResult> GetTubeDetails(string boxId, string position)
        {
            var details = await _sampleService.GetTubeDetailsAsync(boxId, position);
            if (details == null) return NotFound();
            return Ok(details);
        }



        [HttpGet("api/seed")]
        public async Task<IActionResult> SeedDemoData()
        {
            var existing = await _bioBankService.GetAllFreezersAsync();
            if (existing.Any())
                return Ok(new { message = "Already seeded", count = existing.Count });

            var freezers = new[]
            {
        new Bio_CreateFreezerDto
        {
            Id = "FZ80-01",
            Type = FreezerType.Minus80,
            Temperature = -80,
            Barcode = "FZ80-01-BC",
            ShelvesCount = 4,
            RacksPerShelf = 4,
            DrawersPerRack = 5,
            BoxesPerDrawer = 5,
            TubesPerBox = 100
        },
        new Bio_CreateFreezerDto
        {
            Id = "FZ80-02",
            Type = FreezerType.Minus80,
            Temperature = -80,
            Barcode = "FZ80-02-BC",
            ShelvesCount = 4,
            RacksPerShelf = 4,
            DrawersPerRack = 5,
            BoxesPerDrawer = 5,
            TubesPerBox = 100
        },
        new Bio_CreateFreezerDto
        {
            Id = "FZ80-03",
            Type = FreezerType.Minus80,
            Temperature = -80,
            Barcode = "FZ80-03-BC",
            ShelvesCount = 4,
            RacksPerShelf = 4,
            DrawersPerRack = 5,
            BoxesPerDrawer = 5,
            TubesPerBox = 100
        },
        new Bio_CreateFreezerDto
        {
            Id = "FZ80-04",
            Type = FreezerType.Minus80,
            Temperature = -80,
            Barcode = "FZ80-04-BC",
            ShelvesCount = 4,
            RacksPerShelf = 4,
            DrawersPerRack = 5,
            BoxesPerDrawer = 5,
            TubesPerBox = 100
            },
            new Bio_CreateFreezerDto
            {
                Id = "FZ150-01",
                Type = FreezerType.Minus150,
                Temperature = -150,
                Barcode = "FZ150-01-BC",
                ShelvesCount = 0,
                RacksPerShelf = 12,
                DrawersPerRack = 0,
                BoxesPerDrawer = 1,
                TubesPerBox = 100
            },
            new Bio_CreateFreezerDto
            {
                Id = "FZ150-02",
                Type = FreezerType.Minus150,
                Temperature = -150,
                Barcode = "FZ150-02-BC",
                ShelvesCount = 0,
                RacksPerShelf = 12,
                DrawersPerRack = 0,
                BoxesPerDrawer = 1,
                TubesPerBox = 100
            }
         };

            foreach (var dto in freezers)
                await _bioBankService.CreateFreezerAsync(dto);

            return Ok(new { message = "Seeded successfully", count = freezers.Length });
        }

        /// <summary>
        /// Rebuild boxes for a -150°C freezer (e.g. set 12 boxes per rack).
        /// Deletes old boxes under each rack and recreates them.
        /// </summary>
        /// <param name="id">Freezer ID</param>
        /// <param name="boxesPerRack">Optional. Default = freezer.BoxesPerDrawer or 12</param>
        //[HttpPost("api/freezers/{id}/rebuild-boxes")]
        [HttpGet("api/freezers/{id}/rebuild-boxes")]
        public async Task<IActionResult> RebuildBoxes(string id, [FromQuery] int? boxesPerRack = null)
        {
            try
            {
                var result = await _bioBankService.RebuildMinus150BoxesAsync(id, boxesPerRack);
                return Ok(new
                {
                    message = $"Rebuilt boxes for freezer '{id}'",
                    boxesPerRack = result.BoxesPerDrawer,
                    freezer = result
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }





        // ============================================================================
        // INSERT into BioBankController.cs, right after the "TransferSample" endpoint
        // (i.e. right before the "[HttpDelete("api/boxes/{boxId}/tubes/{position}/reserve")]"
        // CancelReserve endpoint, around the current line 342-354).
        // ============================================================================

        [HttpPost("api/samples/checkout")]
        public async Task<IActionResult> CheckOutSample([FromBody] CheckOutSampleDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var result = await _sampleService.CheckOutAsync(dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        }

        [HttpPost("api/samples/checkin")]
        public async Task<IActionResult> CheckInSample([FromBody] CheckInSampleDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var result = await _sampleService.CheckInAsync(dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        }

        /// <summary>
        /// Samples currently out for testing (drives the "checked-out" panel / badge).
        /// </summary>
        [HttpGet("api/samples/checked-out")]
        public async Task<IActionResult> GetActiveCheckOuts()
        {
            var results = await _sampleService.GetActiveCheckOutsAsync();
            return Ok(results);
        }

        /// <summary>
        /// Checked-out samples past their ExpectedReturnAt — for the overdue alert view.
        /// </summary>
        [HttpGet("api/samples/checked-out/overdue")]
        public async Task<IActionResult> GetOverdueCheckOuts()
        {
            var results = await _sampleService.GetOverdueCheckOutsAsync();
            return Ok(results);
        }


        // Add next to the other "SAMPLES" endpoints, e.g. right after GetSample():

        [HttpGet("api/samples/next-id")]
        public async Task<IActionResult> GetNextSampleId()
        {
            var id = await _sampleService.GenerateNextSampleIdAsync();
            return Ok(new { sampleId = id });
        }



        // Add next to the other "SAMPLES" endpoints:

        [HttpPost("api/samples/update")]
        public async Task<IActionResult> UpdateSample([FromBody] UpdateSampleDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var result = await _sampleService.UpdateAsync(dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        }

        [HttpGet("api/boxes/{boxId}/location")]
        public async Task<IActionResult> GetBoxLocation(string boxId)
        {
            var loc = await _sampleService.GetBoxLocationAsync(boxId);
            if (loc == null) return NotFound(new { message = $"Box '{boxId}' not found or has no parent rack." });
            return Ok(loc);
        }

        [HttpGet("api/tubes/next-available")]
        public async Task<IActionResult> GetNextAvailableSlot([FromQuery] string? freezerId = null)
        {
            var slot = await _sampleService.FindNextAvailableSlotAsync(freezerId);
            if (slot == null) return NotFound(new { message = "No available slot found — every online freezer appears full." });
            return Ok(slot);
        }

    }
}