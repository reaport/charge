using ChargeModule.Middlewares;
using ChargeModule.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;

var builder = WebApplication.CreateBuilder(args);

// ���������� �������� ������������
builder.Services.AddControllers();

// ��������� ��������� Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ��������� �����������: ��� ��������� �� ������� �����
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// ����������� HttpClient ��� ������ � API Ground Control
builder.Services.AddHttpClient<IGroundControlClient, GroundControlClient>(client =>
{
    client.BaseAddress = new Uri("https://ground-control.reaport.ru");
});

// ����������� ���������� ����
builder.Services.AddSingleton<IChargeService, ChargeService>();

var app = builder.Build();

// �������� middleware Swagger � ������ ����������
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();

// ��������� ���� middleware ��� ����������� �������� � �������
app.UseMiddleware<RequestResponseLoggingMiddleware>();

app.MapControllers();

// ������������� ��������� ����� ������� ��� ������ ����������
var chargeService = app.Services.GetRequiredService<IChargeService>();
await chargeService.InitializeVehiclesAsync();

app.Run();
