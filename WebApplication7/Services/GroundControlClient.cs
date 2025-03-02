using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ChargeModule.Services
{
    public interface IGroundControlClient
    {
        Task<VehicleRegistrationResponse> RegisterVehicleAsync(string type);
        Task<List<string>> GetRouteAsync(string from, string to, string vehicleType);
        Task<double> RequestMoveAsync(string vehicleId, string vehicleType, string from, string to);
        Task NotifyArrivalAsync(string vehicleId, string vehicleType, string nodeId);
    }

    public class GroundControlClient : IGroundControlClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GroundControlClient> _logger;

        public GroundControlClient(HttpClient httpClient, ILogger<GroundControlClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<VehicleRegistrationResponse> RegisterVehicleAsync(string type)
        {
            // Логируем параметры запроса
            _logger.LogInformation("Отправляем запрос на регистрацию транспортного средства типа: {Тип}", type);

            var response = await _httpClient.PostAsync($"/register-vehicle/{type}", null);
            _logger.LogInformation("Получен ответ на регистрацию, статус: {StatusCode}", response.StatusCode);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<VehicleRegistrationResponse>();
            string responseBody = JsonSerializer.Serialize(result);
            _logger.LogInformation("Ответ на регистрацию: {ResponseBody}", responseBody);
            return result;
        }

        public async Task<List<string>> GetRouteAsync(string from, string to, string vehicleType)
        {
            var requestPayload = new
            {
                from = from,
                to = to,
                type = vehicleType
            };
            string requestBody = JsonSerializer.Serialize(requestPayload);
            _logger.LogInformation("Отправляем запрос маршрута: {RequestBody}", requestBody);

            var response = await _httpClient.PostAsJsonAsync("/route", requestPayload);
            _logger.LogInformation("Получен ответ на запрос маршрута, статус: {StatusCode}", response.StatusCode);
            response.EnsureSuccessStatusCode();
            var route = await response.Content.ReadFromJsonAsync<List<string>>();
            _logger.LogInformation("Полученный маршрут от {From} до {To}: {Route}",
                from, to, route != null ? string.Join(" -> ", route) : "пустой маршрут");
            return route;
        }

        public async Task<double> RequestMoveAsync(string vehicleId, string vehicleType, string from, string to)
        {
            var requestPayload = new
            {
                vehicleId = vehicleId,
                vehicleType = vehicleType,
                from = from,
                to = to
            };
            string requestBody = JsonSerializer.Serialize(requestPayload);
            _logger.LogInformation("Отправляем запрос на перемещение: {RequestBody}", requestBody);

            HttpResponseMessage response = null;
            int retryCount = 0;
            const int maxRetries = 15;

            while (true)
            {
                response = await _httpClient.PostAsJsonAsync("/move", requestPayload);
                _logger.LogInformation("Получен ответ на запрос перемещения, статус: {StatusCode}", response.StatusCode);

                if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    _logger.LogWarning("Статус Conflict при запросе перемещения от {From} до {To} для транспортного средства {VehicleId}. Ожидание 2 секунды и повторная попытка.", from, to, vehicleId);
                    await Task.Delay(TimeSpan.FromSeconds(2));
                    retryCount++;
                    if (retryCount >= maxRetries)
                    {
                        _logger.LogError("Превышено число попыток ({MaxRetries}) для запроса перемещения от {From} до {To} для транспортного средства {VehicleId}.", maxRetries, from, to, vehicleId);
                        break;
                    }
                    continue;
                }
                break;
            }

            response.EnsureSuccessStatusCode();
            var moveResponse = await response.Content.ReadFromJsonAsync<MoveResponse>();
            string responseBody = JsonSerializer.Serialize(moveResponse);
            _logger.LogInformation("Ответ на перемещение: {ResponseBody}", responseBody);
            return moveResponse.Distance;
        }


        public async Task NotifyArrivalAsync(string vehicleId, string vehicleType, string nodeId)
        {
            var requestPayload = new
            {
                vehicleId = vehicleId,
                vehicleType = vehicleType,
                nodeId = nodeId
            };
            string requestBody = JsonSerializer.Serialize(requestPayload);
            _logger.LogInformation("Отправляем уведомление о прибытии: {RequestBody}", requestBody);

            var response = await _httpClient.PostAsJsonAsync("/arrived", requestPayload);
            _logger.LogInformation("Получен ответ на уведомление о прибытии, статус: {StatusCode}", response.StatusCode);
            response.EnsureSuccessStatusCode();
            _logger.LogInformation("Уведомление о прибытии для транспортного средства {VehicleId} получено успешно.", vehicleId);
        }
    }

    public class VehicleRegistrationResponse
    {
        public string GarrageNodeId { get; set; }
        public string VehicleId { get; set; }
        public Dictionary<string, string> ServiceSpots { get; set; }
    }

    public class MoveResponse
    {
        public double Distance { get; set; }
    }
}
