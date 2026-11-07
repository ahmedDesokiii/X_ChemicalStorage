namespace X_ChemicalStorage.Controllers
{
    /// <summary>
    /// Local monitoring station: the screen that runs on the PC hosting the MQTT broker + this app on the LAN.
    /// Shows gateways and their freezers live and sounds the alarm locally (works without internet).
    /// Data comes from GET biobank/api/iot/station.
    /// </summary>
    [Authorize]
    public class StationController : Controller
    {
        [HasPermission(Permissions.IoT.View)]
        [HttpGet("biobank/station")]
        public IActionResult Index() => View();
    }
}
