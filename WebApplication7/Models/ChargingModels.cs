using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ChargeModule.Models
{
    // Запрос на зарядку самолёта
    public class ChargingRequest
    {
        [Required]
        public string NodeId { get; set; }
    }

    // Ответ для запроса зарядки
    public class ChargingResponse
    {
        public bool Wait { get; set; }
    }

    // Запрос на завершение зарядки
    public class ChargingCompletionRequest
    {
        [Required]
        public string NodeId { get; set; }
    }

    // Ответ с ошибкой
    public class ChargingErrorResponse
    {
        public int ErrorCode { get; set; }
        public string Message { get; set; }
    }

    // Состояния сервисного транспорта
    public enum VehicleState
    {
        Free,
        Busy
    }

    // Модель сервисной машины для зарядки
    public class ChargingVehicle
    {
        public string VehicleId { get; set; }
        public string GarrageNodeId { get; set; }
        // Сопоставление: парковка самолёта -> парковочное место машины
        public Dictionary<string, string> ServiceSpots { get; set; }
        public VehicleState State { get; set; }
    }
}
