using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ChargeModule.Models;
using Microsoft.Extensions.Logging;

namespace ChargeModule.Services
{
    public interface IChargeService
    {
        // Инициализация: регистрация машин зарядки
        Task InitializeVehiclesAsync();
        // Обработка запроса на зарядку
        Task<ChargingResponse> ProcessChargingRequestAsync(ChargingRequest request, CancellationToken cancellationToken = default);
        // Завершение зарядки – возврат машины в гараж
        Task ProcessChargingCompletionAsync(ChargingCompletionRequest request);
    }

    public class ChargeService : IChargeService
    {
        private readonly IGroundControlClient _groundControlClient;
        private readonly ILogger<ChargeService> _logger;
        private readonly ConcurrentDictionary<string, ChargingVehicle> _vehicles = new ConcurrentDictionary<string, ChargingVehicle>();
        private readonly object _lock = new object();
        private const int VehicleCount = 3;
        private const string VehicleType = "charging";
        // Скорость движения: единиц расстояния в секунду (например, 10)
        private readonly double _travelSpeed;
        // Словарь для хранения связи между узлом парковки самолёта и назначенной машиной
        private readonly ConcurrentDictionary<string, string> _activeChargingRequests = new ConcurrentDictionary<string, string>();

        public ChargeService(IGroundControlClient groundControlClient, ILogger<ChargeService> logger)
        {
            _groundControlClient = groundControlClient;
            _logger = logger;
            _travelSpeed = 20.0;
        }

        public async Task<ChargingResponse> ProcessChargingRequestAsync(ChargingRequest request, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Обработка запроса зарядки для самолёта с парковкой {NodeId}", request.NodeId);

            // Ищем свободное транспортное средство, которое обслуживает указанный узел
            ChargingVehicle availableVehicle = _vehicles.Values
                .FirstOrDefault(v => v.State == VehicleState.Free && v.ServiceSpots.ContainsKey(request.NodeId));

            if (availableVehicle == null)
            {
                _logger.LogInformation("Нет свободного транспортного средства для парковки {NodeId}. Отправляем ответ wait=true.", request.NodeId);
                return new ChargingResponse { Wait = true };
            }

            bool canAssignVehicle;
            lock (_lock)
            {
                if (availableVehicle.State == VehicleState.Free)
                {
                    availableVehicle.State = VehicleState.Busy;
                    _logger.LogInformation("Транспортное средство {VehicleId} назначено для самолёта с парковкой {NodeId}", availableVehicle.VehicleId, request.NodeId);
                    canAssignVehicle = true;
                }
                else
                {
                    canAssignVehicle = false;
                }
            }
            if (!canAssignVehicle)
            {
                _logger.LogInformation("Транспортное средство уже занято, отправляем ответ wait=true.");
                return new ChargingResponse { Wait = true };
            }

            // При назначении в ProcessChargingRequestAsync, после успешного назначения:
            _activeChargingRequests[request.NodeId] = availableVehicle.VehicleId;

            // Текущая позиция – гараж транспортного средства
            string currentPosition = availableVehicle.GarrageNodeId;
            // Целевая точка – сервисный узел для данного самолёта (например, "parking_1_charging_1")
            string targetPosition = availableVehicle.ServiceSpots[request.NodeId];

            _logger.LogInformation("Запрашиваем маршрут от {CurrentPosition} до {TargetPosition} для транспортного средства {VehicleType}", currentPosition, targetPosition, VehicleType);
            List<string> route = await _groundControlClient.GetRouteAsync(currentPosition, targetPosition, VehicleType);
            if (route == null || route.Count < 2)
            {
                _logger.LogError("Маршрут не найден от {CurrentPosition} до {TargetPosition}", currentPosition, targetPosition);
                throw new Exception("Маршрут не найден");
            }

            _logger.LogInformation("Полученный маршрут от {CurrentPosition} до {TargetPosition}: {Route}", currentPosition, targetPosition, string.Join(" -> ", route));

            // Перемещаем транспортное средство по сегментам маршрута с учетом заданной скорости
            for (int i = 0; i < route.Count - 1; i++)
            {
                string fromNode = route[i];
                string toNode = route[i + 1];

                double distance = await _groundControlClient.RequestMoveAsync(availableVehicle.VehicleId, VehicleType, fromNode, toNode);
                _logger.LogInformation("Запрос перемещения от {FromNode} до {ToNode} разрешен. Расстояние: {Distance}", fromNode, toNode, distance);

                double delaySeconds = distance / _travelSpeed;
                _logger.LogInformation("Ожидание {DelaySeconds} секунд для прохождения сегмента.", delaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);

                await _groundControlClient.NotifyArrivalAsync(availableVehicle.VehicleId, VehicleType, toNode);
            }

            _logger.LogInformation("Транспортное средство {VehicleId} начало зарядку на парковке {TargetPosition}", availableVehicle.VehicleId, targetPosition);
            return new ChargingResponse { Wait = false };
        }

        public async Task ProcessChargingCompletionAsync(ChargingCompletionRequest request)
        {
            _logger.LogInformation("Обработка завершения зарядки для самолёта с парковкой {NodeId}", request.NodeId);

            if (!_activeChargingRequests.TryGetValue(request.NodeId, out var assignedVehicleId))
            {
                _logger.LogError("Нет зарегистрированной машины для завершения зарядки на парковке {NodeId}", request.NodeId);
                throw new Exception("Нет транспортного средства, обслуживающего данный запрос");
            }

            ChargingVehicle vehicle = _vehicles.Values.FirstOrDefault(v => v.VehicleId == assignedVehicleId);
            if (vehicle == null)
            {
                _logger.LogError("Машина с идентификатором {VehicleId} не найдена для завершения зарядки", assignedVehicleId);
                throw new Exception("Нет транспортного средства, обслуживающего данный запрос");
            }

            // Удаляем связь, т.к. запрос завершения обрабатывается
            _activeChargingRequests.TryRemove(request.NodeId, out _);

            // Текущая позиция – сервисный узел, привязанный к данному request.NodeId (например, "parking_1_charging_1")
            string currentPosition = vehicle.ServiceSpots[request.NodeId];
            // Целевая точка – гараж транспортного средства (например, "garrage_charging_1")
            string garageSpot = vehicle.GarrageNodeId;

            _logger.LogInformation("Запрашиваем маршрут от {CurrentPosition} до {GarageSpot} для транспортного средства {VehicleId}",
                currentPosition, garageSpot, vehicle.VehicleId);
            List<string> route = await _groundControlClient.GetRouteAsync(currentPosition, garageSpot, VehicleType);
            if (route == null || route.Count < 1)
            {
                _logger.LogError("Маршрут не найден от {CurrentPosition} до {GarageSpot}", currentPosition, garageSpot);
                throw new Exception("Маршрут не найден");
            }

            _logger.LogInformation("Полученный маршрут от {CurrentPosition} до {GarageSpot}: {Route}",
                currentPosition, garageSpot, string.Join(" -> ", route));

            if (route.Count > 1)
            {
                for (int i = 0; i < route.Count - 1; i++)
                {
                    string fromNode = route[i];
                    string toNode = route[i + 1];

                    double distance = await _groundControlClient.RequestMoveAsync(vehicle.VehicleId, VehicleType, fromNode, toNode);
                    _logger.LogInformation("Запрос перемещения от {FromNode} до {ToNode} разрешен. Расстояние: {Distance}", fromNode, toNode, distance);

                    double delaySeconds = distance / _travelSpeed;
                    _logger.LogInformation("Ожидание {DelaySeconds} секунд для прохождения сегмента.", delaySeconds);
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));

                    await _groundControlClient.NotifyArrivalAsync(vehicle.VehicleId, VehicleType, toNode);
                }
            }
            else
            {
                _logger.LogInformation("Перемещение не требуется, машина уже находится в нужном месте: {CurrentPosition}", currentPosition);
            }

            lock (_lock)
            {
                vehicle.State = VehicleState.Free;
            }
            _logger.LogInformation("Транспортное средство {VehicleId} теперь свободно в гараже.", vehicle.VehicleId);
        }

        public async Task InitializeVehiclesAsync()
        {
            // Регистрируем заданное количество машин
            for (int i = 0; i < VehicleCount; i++)
            {
                try
                {
                    _logger.LogInformation("Инициируем регистрацию транспортного средства №{Номер}", i + 1);
                    var registration = await _groundControlClient.RegisterVehicleAsync(VehicleType);
                    var vehicle = new ChargingVehicle
                    {
                        VehicleId = registration.VehicleId,
                        GarrageNodeId = registration.GarrageNodeId,
                        ServiceSpots = registration.ServiceSpots,
                        State = VehicleState.Free
                    };
                    _vehicles.TryAdd(vehicle.VehicleId, vehicle);
                    _logger.LogInformation("Транспортное средство {VehicleId} зарегистрировано. Гараж: {GarrageNodeId}. ServiceSpots: {ServiceSpots}",
                        vehicle.VehicleId,
                        vehicle.GarrageNodeId,
                        JsonSerializer.Serialize(vehicle.ServiceSpots));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при регистрации транспортного средства №{Номер}", i + 1);
                }
            }

        }

    }
}
