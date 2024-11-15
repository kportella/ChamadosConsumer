using ChamadosConsumer;
using ChamadosConsumer.BackgroundServices;
using ChamadosConsumer.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<WorkerDLQ>();
builder.Services.AddDbContext<ChamadoDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ChamadoDataAccess>();

var host = builder.Build();
host.Run();
