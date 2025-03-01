using ChargeModule.Middlewares;
using ChargeModule.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;

var builder = WebApplication.CreateBuilder(args);

// Добавление сервисов контроллеров
builder.Services.AddControllers();

// Добавляем поддержку Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Настройка логирования: все сообщения на русском языке
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Регистрация HttpClient для работы с API Ground Control
builder.Services.AddHttpClient<IGroundControlClient, GroundControlClient>(client =>
{
    client.BaseAddress = new Uri("https://ground-control.reaport.ru");
});

// Регистрация сервисного слоя
builder.Services.AddSingleton<IChargeService, ChargeService>();

var app = builder.Build();

// Включаем middleware Swagger в режиме разработки
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();

// Добавляем наше middleware для логирования запросов и ответов
app.UseMiddleware<RequestResponseLoggingMiddleware>();

app.MapControllers();

// Инициализация сервисных машин зарядки при старте приложения
var chargeService = app.Services.GetRequiredService<IChargeService>();
await chargeService.InitializeVehiclesAsync();

app.Run();
