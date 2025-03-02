using System;
using System.Threading.Tasks;
using ChargeModule.Models;
using ChargeModule.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ChargeModule.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ChargeController : ControllerBase
    {
        private readonly IChargeService _chargeService;
        private readonly ILogger<ChargeController> _logger;

        public ChargeController(IChargeService chargeService, ILogger<ChargeController> logger)
        {
            _chargeService = chargeService;
            _logger = logger;
        }

        // POST /charge/request
        [HttpPost("request")]
        public async Task<IActionResult> RequestCharging([FromBody] ChargingRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ChargingErrorResponse { ErrorCode = 100, Message = "NodeId is required" });
            }

            try
            {
                var response = await _chargeService.ProcessChargingRequestAsync(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing charging request");
                return StatusCode(500, new ChargingErrorResponse { ErrorCode = 500, Message = "InternalServerError" });
            }
        }

        // POST /charge/complete
        [HttpPost("complete")]
        public async Task<IActionResult> CompleteCharging([FromBody] ChargingCompletionRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ChargingErrorResponse { ErrorCode = 100, Message = "NodeId is required" });
            }

            try
            {
                await _chargeService.ProcessChargingCompletionAsync(request);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing charging");
                return StatusCode(500, new ChargingErrorResponse { ErrorCode = 500, Message = "InternalServerError" });
            }
        }
    }
}
