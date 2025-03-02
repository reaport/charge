using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ChargeModule.Middlewares
{
    public class RequestResponseLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

        public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            // Включаем возможность повторного чтения тела запроса
            context.Request.EnableBuffering();

            // Чтение тела запроса
            string requestBody = string.Empty;
            using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true))
            {
                requestBody = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;
            }

            _logger.LogInformation("Получен запрос: {Method} {Path}\nТело запроса: {RequestBody}",
                context.Request.Method,
                context.Request.Path,
                requestBody);

            // Захватываем ответ для логирования
            var originalBodyStream = context.Response.Body;
            using (var responseBody = new MemoryStream())
            {
                context.Response.Body = responseBody;
                await _next(context);
                context.Response.Body.Seek(0, SeekOrigin.Begin);
                string responseBodyText = await new StreamReader(context.Response.Body).ReadToEndAsync();
                context.Response.Body.Seek(0, SeekOrigin.Begin);
                _logger.LogInformation("Отправлен ответ: {StatusCode}\nТело ответа: {ResponseBody}",
                    context.Response.StatusCode,
                    responseBodyText);
                await responseBody.CopyToAsync(originalBodyStream);
            }
        }
    }
}
