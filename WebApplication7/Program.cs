using ChargeModule.Middlewares;
using ChargeModule.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;

var builder = WebApplication.CreateBuilder(args);

// Настройка Serilog (как в предыдущем примере)
var logFilePath = $"Logs/log-{DateTime.Now:yyyy-MM-dd}.txt";
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddSwaggerGen();

// Добавляем контроллеры и представления
builder.Services.AddControllersWithViews();

// Регистрируем сервисы
builder.Services.AddHttpClient<IGroundControlClient, GroundControlClient>(client =>
{
    client.BaseAddress = new Uri("https://ground-control.reaport.ru");
});
builder.Services.AddSingleton<IAdminConfigService, AdminConfigService>();
builder.Services.AddSingleton<IChargeService, ChargeService>();

var app = builder.Build();

var chargeService = app.Services.GetRequiredService<IChargeService>();
await chargeService.InitializeVehiclesAsync();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseStaticFiles();
app.MapControllers();
app.Run();
