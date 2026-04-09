using Jaina.Caching.Memory;
using Jaina.Samples.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddJainaMemoryCache();
builder.Services.AddHostedService<SampleWorker>();

var host = builder.Build();
host.Run();
