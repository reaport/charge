using ChargeModule.Middlewares;
using ChargeModule.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;

var logFilePath = $"Logs/log-{DateTime.Now:yyyy-MM-dd}.txt";

// ��������� Serilog: ����� ����� � ������� � � ����, ����������� ������� � Information
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information() // ������ Information � ���� (Debug �� ���������)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        logFilePath,
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// ���������� Serilog � �������� ���������� �����������
builder.Host.UseSerilog();

// ��������� ������� ������������
builder.Services.AddControllers();

// ��������� Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ����������� HttpClient ��� ������ � API Ground Control
builder.Services.AddHttpClient<IGroundControlClient, GroundControlClient>(client =>
{
    client.BaseAddress = new Uri("https://ground-control.reaport.ru");
});

// ����������� ���������� ����
builder.Services.AddSingleton<IChargeService, ChargeService>();

var app = builder.Build();

// ���������� Swagger � ������ ����������
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();

// ��������� middleware ��� ����������� �������� � �������
app.UseMiddleware<RequestResponseLoggingMiddleware>();

app.MapControllers();

// ������������� ������������ ������� ��� �������
var chargeService = app.Services.GetRequiredService<IChargeService>();
await chargeService.InitializeVehiclesAsync();

app.Run();
