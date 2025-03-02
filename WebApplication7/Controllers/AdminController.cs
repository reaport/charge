using ChargeModule.Models;
using ChargeModule.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ChargeModule.Controllers
{
    [Route("charge/admin")]
    public class AdminController : Controller
    {
        private readonly IAdminConfigService _adminConfigService;
        private readonly IChargeService _chargeService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(IAdminConfigService adminConfigService, IChargeService chargeService, ILogger<AdminController> logger)
        {
            _adminConfigService = adminConfigService;
            _chargeService = chargeService;
            _logger = logger;
        }

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

        [HttpPost("update")]
        public async Task<IActionResult> Update()
        {
            string requestBody = await ReadRequestBody();
            _logger.LogInformation("Запрос обновления конфигурации:\n{RequestBody}", requestBody);

            var config = new AdminConfig
            {
                MovementSpeed = double.Parse(Request.Form["MovementSpeed"]),
                ConflictRetryCount = int.Parse(Request.Form["ConflictRetryCount"])
            };

            _adminConfigService.UpdateConfig(config);
            _logger.LogInformation("Конфигурация обновлена: скорость = {MovementSpeed}, попыток = {ConflictRetryCount}", config.MovementSpeed, config.ConflictRetryCount);
            return RedirectToAction("Index");
        }

        [HttpPost("registerVehicle")]
        public async Task<IActionResult> RegisterVehicle()
        {
            string requestBody = await ReadRequestBody();
            _logger.LogInformation("Запрос регистрации транспортного средства:\n{RequestBody}", requestBody);

            string type = Request.Form["Type"];
            await _chargeService.RegisterVehicleAsync(type);
            _logger.LogInformation("Запрос на регистрацию транспортного средства типа {Type} успешно обработан.", type);

            return RedirectToAction("Index");
        }

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
