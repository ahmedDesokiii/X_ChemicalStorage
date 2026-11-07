using Microsoft.AspNetCore.Mvc;
using X_ChemicalStorage.BioBank.DTOs;
using X_ChemicalStorage.BioBank.Enums;
using X_ChemicalStorage.BioBank.Services;
using Microsoft.AspNetCore.Authorization;
using X_ChemicalStorage.BioBank.Security;

namespace X_ChemicalStorage.Controllers
{
    [Authorize]
    [Route("biobank")]
    public class BioBankController : Controller
    {
        private readonly IBioBankService _bioBankService;
        private readonly ISampleService _sampleService;
        private readonly IFreezerIoTService _ioTService;
        private readonly IBioAttachmentService _attachmentService;
        

        public BioBankController(IBioBankService bioBankService, ISampleService sampleService, IFreezerIoTService ioTService, IBioAttachmentService attachmentService)
        {
            _bioBankService = bioBankService;
            _sampleService = sampleService;
            _ioTService = ioTService;
            _attachmentService = attachmentService;
        }

        // NEW — resolves the real logged-in user instead of trusting whatever the client sends.
        // Falls back to whatever the client provided only if there's no authenticated identity.
        private string ResolvePerformedBy(string? fallback)
        {
            var identityName = User?.Identity?.IsAuthenticated == true ? User.Identity!.Name : null;
            if (!string.IsNullOrWhiteSpace(identityName)) return identityName;
            return string.IsNullOrWhiteSpace(fallback) ? "Unknown user" : fallback;
        }

        // ==================== VIEW ====================

        [HasPermission(Permissions.BioBank.View)]
        [HttpGet]
        [Route("")]
        [Route("index")]
        public IActionResult Index()
        {
            ViewData["Title"] = "Bio Bank Storage";
            return View();
        }
        [HasPermission(Permissions.BioBank.View)]
        public IActionResult demo()
        {
            return View();
        }

        // ==================== FREEZERS ====================

        /// <summary>
        /// Get all freezers (Room view)
        /// </summary>
        [HasPermission(Permissions.BioBank.View)]
        [HttpGet("api/freezers")]
        public async Task<IActionResult> GetAllFreezers()
        {
            var freezers = await _bioBankService.GetAllFreezersAsync();
            return Ok(freezers);
        }

        /// <summary>
        /// Get single freezer by ID
        /// </summary>
        [HasPermission(Permissions.BioBank.View)]
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
        [HasPermission(Permissions.BioBank.ManageFreezers)]
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
        [HasPermission(Permissions.BioBank.ManageFreezers)]
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
        [HasPermission(Permissions.BioBank.View)]
        [HttpGet("api/freezers/{freezerId}/shelves")]
        public async Task<IActionResult> GetShelves(string freezerId)
        {
            var shelves = await _bioBankService.GetShelvesByFreezerAsync(freezerId);
            return Ok(shelves);
        }

        /// <summary>
        /// Get racks under a specific shelf (-80°C)
        /// </summary>
        [HasPermission(Permissions.BioBank.View)]
        [HttpGet("api/freezers/{freezerId}/shelves/{shelfIndex:int}/racks")]
        public async Task<IActionResult> GetRacksByShelf(string freezerId, int shelfIndex)
        {
            var racks = await _bioBankService.GetRacksByShelfAsync(freezerId, shelfIndex);
            return Ok(racks);
        }

        /// <summary>
        /// Get racks of a -150°C freezer (no shelves)
        /// </summary>
        [HasPermission(Permissions.BioBank.View)]
        [HttpGet("api/freezers/{freezerId}/racks")]
        public async Task<IActionResult> GetRacksByFreezer(string freezerId)
        {
            var racks = await _bioBankService.GetRacksByFreezerAsync(freezerId);
            return Ok(racks);
        }

        /// <summary>
        /// Get drawers under a rack
        /// </summary>
        [HasPermission(Permissions.BioBank.View)]
        [HttpGet("api/racks/{rackId}/drawers")]
        public async Task<IActionResult> GetDrawers(string rackId)
        {
            var drawers = await _bioBankService.GetDrawersByRackAsync(rackId);
            return Ok(drawers);
        }

        /// <summary>
        /// Get boxes under a drawer (-80°C)
        /// </summary>
        [HasPermission(Permissions.BioBank.View)]
        [HttpGet("api/drawers/{drawerId}/boxes")]
        public async Task<IActionResult> GetBoxesByDrawer(string drawerId)
        {
            var boxes = await _bioBankService.GetBoxesByDrawerAsync(drawerId);
            return Ok(boxes);
        }

        /// <summary>
        /// Get boxes under a rack (-150°C)
        /// </summary>
        [HasPermission(Permissions.BioBank.View)]
        [HttpGet("api/racks/{rackId}/boxes")]
        public async Task<IActionResult> GetBoxesByRack(string rackId)
        {
            var boxes = await _bioBankService.GetBoxesByRackAsync(rackId);
            return Ok(boxes);
        }

        /// <summary>
        /// Get full box details + all tubes (10×10 grid)
        /// </summary>
        [HasPermission(Permissions.BioBank.View)]
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
        [HasPermission(Permissions.BioBank.View)]
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
        [HasPermission(Permissions.BioBank.View)]
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
        [HasPermission(Permissions.BioBank.Edit)]
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
        [HasPermission(Permissions.BioBank.Store)]
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
        [HasPermission(Permissions.BioBank.Store)]
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
        [HasPermission(Permissions.BioBank.Reserve)]
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
        [HasPermission(Permissions.BioBank.Withdraw)]
        [HttpDelete("api/boxes/{boxId}/tubes/{position}")]
        public async Task<IActionResult> ClearTube(string boxId, string position)
        {
            var cleared = await _bioBankService.ClearTubeAsync(boxId, position);
            if (!cleared)
                return NotFound(new { message = $"Tube at position '{position}' in box '{boxId}' not found." });

            return NoContent();
        }

        
        // ========== SAMPLES ==========

        [HasPermission(Permissions.BioBank.Store)]
        [HttpPost("api/samples/store")]
        public async Task<IActionResult> StoreSample([FromBody] StoreSampleDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            dto.PerformedBy = ResolvePerformedBy(dto.PerformedBy);
            try
            {
                var result = await _sampleService.StoreAsync(dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HasPermission(Permissions.BioBank.Reserve)]
        [HttpPost("api/samples/reserve")]
        public async Task<IActionResult> ReserveSample([FromBody] ReserveSampleDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            dto.PerformedBy = ResolvePerformedBy(dto.PerformedBy);
            try
            {
                var result = await _sampleService.ReserveAsync(dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HasPermission(Permissions.BioBank.Withdraw)]
        [HttpPost("api/samples/withdraw")]
        public async Task<IActionResult> WithdrawSample([FromBody] WithdrawSampleDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            dto.PerformedBy = ResolvePerformedBy(dto.PerformedBy);
            try
            {
                var result = await _sampleService.WithdrawAsync(dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        }

        [HasPermission(Permissions.BioBank.Transfer)]
        [HttpPost("api/samples/transfer")]
        public async Task<IActionResult> TransferSample([FromBody] TransferSampleDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            dto.PerformedBy = ResolvePerformedBy(dto.PerformedBy);
            try
            {
                var result = await _sampleService.TransferAsync(dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HasPermission(Permissions.BioBank.Reserve)]
        [HttpDelete("api/boxes/{boxId}/tubes/{position}/reserve")]
        public async Task<IActionResult> CancelReserve(string boxId, string position, [FromQuery] string? performedBy = null)
        {
            performedBy = ResolvePerformedBy(performedBy);
            var ok = await _sampleService.CancelReserveAsync(boxId, position, performedBy);
            if (!ok) return NotFound(new { message = "No active reservation found." });
            return NoContent();
        }

        [HasPermission(Permissions.BioBank.View)]
        [HttpGet("api/samples/{id}")]
        public async Task<IActionResult> GetSample(string id)
        {
            var s = await _sampleService.GetByIdAsync(id);
            if (s == null) return NotFound(new { message = "Sample not found." });
            return Ok(s);
        }

        [HasPermission(Permissions.BioBank.View)]
        [HttpGet("api/samples/{id}/history")]
        public async Task<IActionResult> GetSampleHistory(string id)
        {
            var history = await _sampleService.GetHistoryAsync(id);
            return Ok(history);
        }

        [HasPermission(Permissions.BioBank.View)]
        [HttpGet("api/samples/search")]
        public async Task<IActionResult> SearchSamples([FromQuery] string q)
        {
            var results = await _sampleService.SearchAsync(q ?? "");
            return Ok(results);
        }

        [HasPermission(Permissions.BioBank.View)]
        [HttpGet("api/boxes/{boxId}/tubes/{position}")]
        public async Task<IActionResult> GetTubeDetails(string boxId, string position)
        {
            var details = await _sampleService.GetTubeDetailsAsync(boxId, position);
            if (details == null) return NotFound();
            return Ok(details);
        }



        [HasPermission(Permissions.BioBank.ManageFreezers)]
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
        [HasPermission(Permissions.BioBank.ManageFreezers)]
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

        [HasPermission(Permissions.BioBank.CheckInOut)]
        [HttpPost("api/samples/checkout")]
        public async Task<IActionResult> CheckOutSample([FromBody] CheckOutSampleDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            dto.PerformedBy = ResolvePerformedBy(dto.PerformedBy);
            try
            {
                var result = await _sampleService.CheckOutAsync(dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        }

        [HasPermission(Permissions.BioBank.CheckInOut)]
        [HttpPost("api/samples/checkin")]
        public async Task<IActionResult> CheckInSample([FromBody] CheckInSampleDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            dto.PerformedBy = ResolvePerformedBy(dto.PerformedBy);
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
        [HasPermission(Permissions.BioBank.View)]
        [HttpGet("api/samples/checked-out")]
        public async Task<IActionResult> GetActiveCheckOuts()
        {
            var results = await _sampleService.GetActiveCheckOutsAsync();
            return Ok(results);
        }

        /// <summary>
        /// Checked-out samples past their ExpectedReturnAt — for the overdue alert view.
        /// </summary>
        [HasPermission(Permissions.BioBank.View)]
        [HttpGet("api/samples/checked-out/overdue")]
        public async Task<IActionResult> GetOverdueCheckOuts()
        {
            var results = await _sampleService.GetOverdueCheckOutsAsync();
            return Ok(results);
        }


        // Add next to the other "SAMPLES" endpoints, e.g. right after GetSample():

        [HasPermission(Permissions.BioBank.View)]
        [HttpGet("api/samples/next-id")]
        public async Task<IActionResult> GetNextSampleId()
        {
            var id = await _sampleService.GenerateNextSampleIdAsync();
            return Ok(new { sampleId = id });
        }



        // Add next to the other "SAMPLES" endpoints:

        [HasPermission(Permissions.BioBank.Edit)]
        [HttpPost("api/samples/update")]
        public async Task<IActionResult> UpdateSample([FromBody] UpdateSampleDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            dto.PerformedBy = ResolvePerformedBy(dto.PerformedBy);
            try
            {
                var result = await _sampleService.UpdateAsync(dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        }

        [HasPermission(Permissions.BioBank.View)]
        [HttpGet("api/boxes/{boxId}/location")]
        public async Task<IActionResult> GetBoxLocation(string boxId)
        {
            var loc = await _sampleService.GetBoxLocationAsync(boxId);
            if (loc == null) return NotFound(new { message = $"Box '{boxId}' not found or has no parent rack." });
            return Ok(loc);
        }

        [HasPermission(Permissions.BioBank.View)]
        [HttpGet("api/tubes/next-available")]
        public async Task<IActionResult> GetNextAvailableSlot([FromQuery] string? freezerId = null)
        {
            var slot = await _sampleService.FindNextAvailableSlotAsync(freezerId);
            if (slot == null) return NotFound(new { message = "No available slot found — every online freezer appears full." });
            return Ok(slot);
        }

        [HasPermission(Permissions.BioBank.View)]
        [HttpGet("api/sops")]
        public async Task<IActionResult> GetSOPs()
        {
            var sops = await _sampleService.GetSOPsAsync();
            return Ok(sops);
        }

        [HasPermission(Permissions.BioBank.ManageSOPs)]
        [HttpPost("api/sops")]
        public async Task<IActionResult> CreateSOP([FromBody] CreateSOPDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var result = await _sampleService.CreateSOPAsync(dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        }



        // ==================== ATTACHMENTS (NEW) ====================

        [HasPermission(Permissions.BioBank.Attachments)]
        [HttpPost("api/samples/{sampleId}/attachments")]
        [RequestSizeLimit(105_000_000)] // ~100MB + small buffer for multipart overhead
        public async Task<IActionResult> UploadAttachment(string sampleId, [FromQuery] AttachmentCategory category, IFormFile file)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var performedBy = ResolvePerformedBy(null);
                var result = await _attachmentService.UploadAsync(sampleId, category, file, performedBy);
                return Ok(result);
            }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        }

        [HasPermission(Permissions.BioBank.View)]
        [HttpGet("api/samples/{sampleId}/attachments")]
        public async Task<IActionResult> GetAttachments(string sampleId, [FromQuery] AttachmentCategory? category = null)
        {
            var list = await _attachmentService.GetAttachmentsAsync(sampleId, category);
            return Ok(list);
        }

        [HasPermission(Permissions.BioBank.View)]
        [HttpGet("api/attachments/{id}/download")]
        public async Task<IActionResult> DownloadAttachment(string id)
        {
            var result = await _attachmentService.DownloadAsync(id);
            if (result == null) return NotFound(new { message = "Attachment not found." });
            return File(result.Stream, result.ContentType, result.FileName);
        }

        [HasPermission(Permissions.BioBank.Attachments)]
        [HttpDelete("api/attachments/{id}")]
        public async Task<IActionResult> DeleteAttachment(string id)
        {
            var deleted = await _attachmentService.DeleteAsync(id);
            if (!deleted) return NotFound(new { message = "Attachment not found." });
            return NoContent();
        }


        // ==================== IoT ====================

        [HttpPost("api/iot/ingest")]
        [AllowAnonymous]                            
        [IngestApiKey]
        public async Task<IActionResult> IngestReading([FromBody] IngestReadingDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _ioTService.ProcessIncomingReadingAsync(dto);

            // Show unlinked collectors on the station screen so they can be linked with one click
            var station = HttpContext.RequestServices.GetRequiredService<X_ChemicalStorage.BioBank.Services.StationState>();
            if (result == IngestResult.UnknownDevice)
                station.UnlinkedCollectorSeen(dto.CollectorCode, "http/ingest", dto.Temperature, DateTime.UtcNow);
            else
                station.CollectorLinked(dto.CollectorCode);

            return Ok(new { message = "Reading processed", result = result.ToString() });
        }

        /// Current IoT link of a freezer (204 when not linked) — used to pre-fill the Link Device dialog.
        [HasPermission(Permissions.IoT.View, Permissions.BioBank.View)]
        [HttpGet("api/freezers/{id}/iot-device")]
        public async Task<IActionResult> GetIoTDevice(string id)
        {
            var device = await _ioTService.GetDeviceByFreezerAsync(id);
            return device == null ? NoContent() : Ok(device);
        }

        [HasPermission(Permissions.IoT.ManageDevices)]
        [HttpDelete("api/freezers/{id}/iot-device")]
        public async Task<IActionResult> UnlinkIoTDevice(string id)
        {
            return await _ioTService.UnlinkDeviceAsync(id)
                ? Ok(new { message = $"IoT device unlinked from {id}." })
                : NotFound(new { message = $"Freezer '{id}' has no linked device." });
        }

        [HasPermission(Permissions.IoT.ManageDevices)]
        [HttpPost("api/iot/link")]
        public async Task<IActionResult> LinkDevice([FromBody] LinkIoTDeviceDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var result = await _ioTService.LinkDeviceAsync(dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        [HasPermission(Permissions.IoT.View, Permissions.BioBank.View)]
        [HttpGet("api/freezers/{id}/readings")]
        public async Task<IActionResult> GetReadings(string id, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
        {
            var fromDate = from ?? DateTime.UtcNow.AddDays(-7);
            var toDate = to ?? DateTime.UtcNow;
            var list = await _ioTService.GetHistoricalAsync(id, fromDate, toDate);
            return Ok(list);
        }

        [HasPermission(Permissions.IoT.View, Permissions.BioBank.View)]
        [HttpGet("api/freezers/{id}/alarms")]
        public async Task<IActionResult> GetAlarms(string id)
        {
            var list = await _ioTService.GetActiveAlarmsAsync(id);
            return Ok(list);
        }


        // ===== في BioBankController.cs: استبدل أكشن AcknowledgeAlarm =====

        [HasPermission(Permissions.IoT.AcknowledgeAlarms)]
        [HttpPost("api/alarms/{id}/acknowledge")]
        public async Task<IActionResult> AcknowledgeAlarm(long id, [FromBody] AckAlarmDto? dto)
        {
            var user = ResolvePerformedBy(null);
            try
            {
                await _ioTService.AcknowledgeAlarmAsync(id, user, dto?.Note);
                return Ok(new { message = "Acknowledged" });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }


        //////////// Test 
        ///
        /// <summary>
        /// تجربة سريعة من البراوزر — امسح بعد الاختبار

        [HasPermission(Permissions.IoT.ManageDevices)]
        [HttpGet("api/iot/test-ingest")]
        [TestModeOnly]
        public async Task<IActionResult> TestIngest(
            [FromQuery] string collector = "GF5022315032881",
            [FromQuery] decimal? temp = null,
            [FromQuery] bool online = true,
            [FromQuery] string door = "Closed",
            [FromQuery] string power = "OK")
        {
            var result = await _ioTService.ProcessIncomingReadingAsync(new IngestReadingDto
            {
                CollectorCode = collector,
                Temperature = temp,
                IsOnline = online,
                DoorStatus = door,
                PowerStatus = power
            });

            var station = HttpContext.RequestServices.GetRequiredService<StationState>();
            if (result == IngestResult.UnknownDevice)
            {
                station.UnlinkedCollectorSeen(collector, "test-ingest", temp, DateTime.UtcNow);
                return NotFound(new { message = $"Collector '{collector}' is not linked to any active freezer. It is now listed on the Station screen for linking." });
            }
            station.CollectorLinked(collector);

            return Ok(new { message = "Reading ingested", result = result.ToString(), collector, temperature = temp, door, power });
        }


        [HttpGet("api/iot/db-info")]

        [HasPermission(Permissions.IoT.ManageDevices)]
        public async Task<IActionResult> DbInfo([FromServices] ApplicationDbContext db)
        {
            var c = db.Database.GetDbConnection();
            return Ok(new
            {
                server = c.DataSource,
                database = c.Database,
                devices = await db.Bio_IoTDevices
                    .Select(d => new { d.Id, d.FreezerId, d.CollectorCode, d.IsActive }).ToListAsync(),
                freezers = await db.Bio_Freezers
                    .Select(f => new { f.Id, f.Status, f.CurrentTemperature, f.LastReadingAt }).ToListAsync()
            });
        }


        /// <summary>
        /// ربط سريع من البراوزر — امسحه بعد الاختبار
        /// مثال: /biobank/api/iot/test-link?freezerId=FZ80-01&collector=GF5022315032881&serial=GF50222308030161
        /// </summary>
        [HasPermission(Permissions.IoT.ManageDevices)]
        [HttpGet("api/iot/test-link")]
        public async Task<IActionResult> TestLink(
            [FromQuery] string freezerId = "FZ80-01",
            [FromQuery] string collector = "GF5022315032881",
            [FromQuery] string serial = "GF50222308030161")
        {
            try
            {
                var result = await _ioTService.LinkDeviceAsync(new LinkIoTDeviceDto
                {
                    FreezerId = freezerId,
                    CollectorCode = collector,
                    GatewaySerial = serial,
                    DeviceName = "Test Gateway"
                });
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

    }
}