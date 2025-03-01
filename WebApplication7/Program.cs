using ChargeModule.Middlewares;
using ChargeModule.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;

var logFilePath = $"Logs/log-{DateTime.Now:yyyy-MM-dd}.txt";

// Настройка Serilog: вывод логов в консоль и в файл, минимальный уровень – Information
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information() // Только Information и выше (Debug не выводятся)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        logFilePath,
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Используем Serilog в качестве провайдера логирования
builder.Host.UseSerilog();

// Добавляем сервисы контроллеров
builder.Services.AddControllers();

// Поддержка Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Регистрация HttpClient для работы с API Ground Control
builder.Services.AddHttpClient<IGroundControlClient, GroundControlClient>(client =>
{
    client.BaseAddress = new Uri("https://ground-control.reaport.ru");
});

// Регистрация сервисного слоя
builder.Services.AddSingleton<IChargeService, ChargeService>();

var app = builder.Build();

// Подключаем Swagger в режиме разработки
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();

// Добавляем middleware для логирования запросов и ответов
app.UseMiddleware<RequestResponseLoggingMiddleware>();

app.MapControllers();

// Инициализация транспортных средств при запуске
var chargeService = app.Services.GetRequiredService<IChargeService>();
await chargeService.InitializeVehiclesAsync();

app.Run();
