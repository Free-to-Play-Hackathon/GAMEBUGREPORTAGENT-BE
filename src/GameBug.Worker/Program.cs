using GameBug.Application;
using GameBug.Infrastructure;
using GameBug.Worker.Consumers;
using GameBug.Worker.HostedServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<OutboxDispatcherService>();
builder.Services.AddHostedService<ProcessAnalysisJobService>();

var host = builder.Build();

await host.RunAsync();
