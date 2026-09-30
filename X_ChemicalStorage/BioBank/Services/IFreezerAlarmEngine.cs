using X_ChemicalStorage.BioBank.DTOs;
using X_ChemicalStorage.Models;

namespace X_ChemicalStorage.BioBank.Services
{
    public interface IFreezerAlarmEngine
    {
        /// يقيّم قراءة واردة. لا يحفظ (الحفظ مسؤولية المستدعي) ثم يستدعي FlushNotificationsAsync.
        Task EvaluateReadingAsync(Bio_Freezer freezer, IngestReadingDto dto, DateTime now);

        /// فحوصات دورية بدون رسايل: انقطاع الاتصال + التصعيد. بيحفظ بنفسه.
        Task RunPeriodicChecksAsync(DateTime now, CancellationToken ct = default);

        /// يرسل الإشعارات المتراكمة (بعد نجاح الحفظ).
        Task FlushNotificationsAsync(CancellationToken ct = default);
    }
}