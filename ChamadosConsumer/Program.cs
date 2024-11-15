using ChamadosConsumer;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<WorkerDLQ>();
builder.Services.AddDbContext<ChamadoDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

var host = builder.Build();
host.Run();
