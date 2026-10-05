using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using X_ChemicalStorage.BioBank.DTOs;
using X_ChemicalStorage.BioBank.Enums;
using X_ChemicalStorage.BioBank.Options;
using X_ChemicalStorage.Data;
using X_ChemicalStorage.Models;

namespace X_ChemicalStorage.BioBank.Services
{
    public interface IIoTDashboardService
    {
        Task<IoTDashboardDto> GetDashboardAsync(int days, DateTime? nowUtc = null);
        Task<FreezerSettingsDto?> GetSettingsAsync(string freezerId);
        /// يرمي ArgumentException برسالة واضحة لو القيم غير صالحة، وKeyNotFoundException لو الفريزر مش موجود.
        Task<FreezerSettingsDto> UpdateSettingsAsync(string freezerId, UpdateFreezerSettingsDto dto, string performedBy);
        Task<List<SettingsAuditDto>> GetSettingsAuditAsync(string freezerId, int take = 100);
    }

}