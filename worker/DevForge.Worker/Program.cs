using DevForge.Application;
using DevForge.Infrastructure;
using DevForge.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDeploymentPipeline();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSimulatedStageExecution(builder.Configuration);

builder.Services.AddOptions<WorkerOptions>()
    .Bind(builder.Configuration.GetSection(WorkerOptions.SectionName))
    .Validate(options => options.PollingInterval > TimeSpan.Zero, "Worker:PollingInterval must be positive.")
    .ValidateOnStart();

builder.Services.AddHostedService<DeploymentWorker>();

var host = builder.Build();
await host.RunAsync();
