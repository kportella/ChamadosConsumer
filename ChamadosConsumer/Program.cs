using ChamadosConsumer;
using ChamadosConsumer.BackgroundServices;
using ChamadosConsumer.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
// Cria Worker como um BackgroundService
builder.Services.AddHostedService<Worker>();
// Cria WorkerDLQ como BackgroundService
builder.Services.AddHostedService<WorkerDLQ>();
// Configura EntityFramework
builder.Services.AddDbContext<ChamadoDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ChamadoDataAccess>();

var host = builder.Build();
host.Run();
