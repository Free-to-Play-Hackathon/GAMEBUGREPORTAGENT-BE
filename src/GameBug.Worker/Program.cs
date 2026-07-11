using GameBug.Application;
using GameBug.Infrastructure;
using GameBug.Infrastructure.Jobs;
using GameBug.Worker.Consumers;
using GameBug.Worker.HostedServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<OutboxDispatcherService>();
builder.Services.AddHostedService<ProcessAnalysisJobService>();
builder.Services.AddHostedService<IndexHistoricalTicketJob>();

var host = builder.Build();

await host.RunAsync();
