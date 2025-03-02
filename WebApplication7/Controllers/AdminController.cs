using ChargeModule.Models;
using ChargeModule.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ChargeModule.Controllers
{
    // Контроллер для администрирования системы зарядки
    [Route("charge/admin")]
    public class AdminController : Controller
    {
        private readonly IAdminConfigService _adminConfigService;
        private readonly IChargeService _chargeService;
        private readonly ILogger<AdminController> _logger;

        // Конструктор контроллера, принимает зависимости через DI
        public AdminController(IAdminConfigService adminConfigService, IChargeService chargeService, ILogger<AdminController> logger)
        {
            _adminConfigService = adminConfigService;
            _chargeService = chargeService;
            _logger = logger;
        }

        // Отображает страницу админки (GET /charge/admin)
        [HttpGet]
        public IActionResult Index()
        {
            var model = new AdminViewModel
            {
                Config = _adminConfigService.GetConfig(),
                Vehicles = _chargeService.GetVehiclesInfo()
            };
            return View(model);
        }

        // Обновляет параметры конфигурации (POST /charge/admin/update)
        [HttpPost("update")]
        public async Task<IActionResult> Update()
        {
            // Логируем тело запроса
            string requestBody = await ReadRequestBody();
            _logger.LogInformation("Запрос обновления конфигурации:\n{RequestBody}", requestBody);

            // Обновляем конфигурацию
            var config = new AdminConfig
            {
                MovementSpeed = double.Parse(Request.Form["MovementSpeed"]),
                ConflictRetryCount = int.Parse(Request.Form["ConflictRetryCount"])
            };

            _adminConfigService.UpdateConfig(config);
            _logger.LogInformation("Конфигурация обновлена: скорость = {MovementSpeed}, попыток = {ConflictRetryCount}", config.MovementSpeed, config.ConflictRetryCount);
            return RedirectToAction("Index");
        }

        // Запрос на регистрацию новой машины (POST /charge/admin/registerVehicle)
        [HttpPost("registerVehicle")]
        public async Task<IActionResult> RegisterVehicle()
        {
            // Логируем тело запроса
            string requestBody = await ReadRequestBody();
            _logger.LogInformation("Запрос регистрации транспортного средства:\n{RequestBody}", requestBody);

            // Извлекаем тип машины из формы
            string type = Request.Form["Type"];
            await _chargeService.RegisterVehicleAsync(type);
            _logger.LogInformation("Запрос на регистрацию транспортного средства типа {Type} успешно обработан.", type);

            return RedirectToAction("Index");
        }

        // Метод для чтения тела запроса
        private async Task<string> ReadRequestBody()
        {
            Request.EnableBuffering();
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true))
            {
                string body = await reader.ReadToEndAsync();
                Request.Body.Position = 0;
                return body;
            }
        }
    }
}
