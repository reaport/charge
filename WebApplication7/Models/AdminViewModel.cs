using System.Collections.Generic;

namespace ChargeModule.Models
{
    public class AdminViewModel
    {
        public AdminConfig Config { get; set; }
        // Список машин на карте
        public IEnumerable<ChargingVehicleInfo> Vehicles { get; set; }
    }

    // DTO для отображения информации о машине
    public class ChargingVehicleInfo
    {
        public string VehicleId { get; set; }
        public string CurrentNode { get; set; }
        public string Status { get; set; }
    }
}
