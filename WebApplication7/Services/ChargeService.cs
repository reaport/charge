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
        // Количество регистрируемых машин
        private const int VehicleCount = 3;
        // Тип транспорта для зарядки (согласно спецификации ground control)
        private const string VehicleType = "charging";

        public ChargeService(IGroundControlClient groundControlClient, ILogger<ChargeService> logger)
        {
            _groundControlClient = groundControlClient;
            _logger = logger;
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

        public async Task<ChargingResponse> ProcessChargingRequestAsync(ChargingRequest request, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Processing charging request for airplane node {NodeId}", request.NodeId);
            ChargingVehicle availableVehicle = null;

            // Ждем, пока появится свободная машина
            while (availableVehicle == null)
            {
                availableVehicle = _vehicles.Values.FirstOrDefault(v => v.State == VehicleState.Free && v.ServiceSpots.ContainsKey(request.NodeId));
                if (availableVehicle == null)
                {
                    // Если машины нет, возвращаем wait:true и ждем 2 секунды
                    _logger.LogInformation("No free vehicle available for airplane node {NodeId}. Waiting...", request.NodeId);
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                }
            }

            bool canAssignVehicle;
            lock (_lock)
            {
                if (availableVehicle.State == VehicleState.Free)
                {
                    availableVehicle.State = VehicleState.Busy;
                    _logger.LogInformation("Vehicle {VehicleId} assigned to airplane node {NodeId}", availableVehicle.VehicleId, request.NodeId);
                    canAssignVehicle = true;
                }
                else
                {
                    canAssignVehicle = false;
                }
            }

            if (!canAssignVehicle)
            {
                // Если в промежутке состояние изменилось – повторно обрабатываем запрос
                return await ProcessChargingRequestAsync(request, cancellationToken);
            }


            // Получаем парковочное место для машины для данного самолёта
            string serviceSpot = availableVehicle.ServiceSpots[request.NodeId];

            // Определяем текущую позицию машины (она находится в гараже)
            string currentPosition = availableVehicle.GarrageNodeId;
            // Получаем целевую точку из сопоставления: парковка для самолёта с учетом идентификатора машины
            string targetPosition = availableVehicle.ServiceSpots[request.NodeId];

            // Запрашиваем маршрут от текущей позиции до целевой точки
            List<string> route = await _groundControlClient.GetRouteAsync(currentPosition, targetPosition, VehicleType);
            if (route == null || route.Count < 2)
            {
                _logger.LogError("Маршрут не найден от {From} до {To}", currentPosition, targetPosition);
                throw new Exception("Маршрут не найден");
            }


            // Последовательно проходим маршрут. Для простоты будем двигаться парами узлов.
            for (int i = 0; i < route.Count - 1; i++)
            {
                string currentNode = route[i];
                string nextNode = route[i + 1];

                // Запрашиваем разрешение на перемещение
                await _groundControlClient.RequestMoveAsync(availableVehicle.VehicleId, VehicleType, currentNode, nextNode);
                // Имитация движения – ожидание прибытия (можно заменить логикой определения времени движения)
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                // Уведомляем о прибытии
                await _groundControlClient.NotifyArrivalAsync(availableVehicle.VehicleId, VehicleType, nextNode);
            }

            // После прибытия запускаем процесс зарядки (в реальной системе здесь запускается логика зарядки)
            _logger.LogInformation("Vehicle {VehicleId} started charging at airplane node {NodeId}", availableVehicle.VehicleId, request.NodeId);

            // Возвращаем ответ, что ожидание не требуется
            return new ChargingResponse { Wait = false };
        }

        public async Task ProcessChargingCompletionAsync(ChargingCompletionRequest request)
        {
            _logger.LogInformation("Обработка завершения зарядки для самолёта с парковкой {NodeId}", request.NodeId);

            // Находим транспорт, обслуживающий данный самолёт (предполагается, что связь сохранена)
            ChargingVehicle vehicle = _vehicles.Values.FirstOrDefault(v => v.State == VehicleState.Busy && v.ServiceSpots.ContainsKey(request.NodeId));
            if (vehicle == null)
            {
                _logger.LogError("Не найден транспорт, обслуживающий самолёт с парковкой {NodeId}", request.NodeId);
                throw new Exception("Нет транспортного средства, обслуживающего данный самолёт");
            }

            // Текущая позиция транспортного средства – это его сервисное парковочное место для данного самолёта
            string currentPosition = vehicle.ServiceSpots[request.NodeId];

            // Целевая точка – гараж транспортного средства
            string garageSpot = vehicle.GarrageNodeId;

            // Запрашиваем маршрут от текущей позиции до гаража
            List<string> route = await _groundControlClient.GetRouteAsync(currentPosition, garageSpot, VehicleType);
            if (route == null || route.Count < 2)
            {
                _logger.LogError("Маршрут не найден от {From} до {To}", currentPosition, garageSpot);
                throw new Exception("Маршрут не найден");
            }

            // Проходим маршрут поэтапно
            for (int i = 0; i < route.Count - 1; i++)
            {
                string currentNode = route[i];
                string nextNode = route[i + 1];

                await _groundControlClient.RequestMoveAsync(vehicle.VehicleId, VehicleType, currentNode, nextNode);
                await Task.Delay(TimeSpan.FromSeconds(1));
                await _groundControlClient.NotifyArrivalAsync(vehicle.VehicleId, VehicleType, nextNode);
            }

            // По прибытии в гараж – помечаем машину как свободную
            lock (_lock)
            {
                vehicle.State = VehicleState.Free;
            }
            _logger.LogInformation("Транспортное средство {VehicleId} теперь свободно в гараже", vehicle.VehicleId);
        }

    }
}
