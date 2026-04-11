using Jaina.Caching.Memory;
using Jaina.Samples.ServiceDefaults;
using Jaina.Samples.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddJainaMemoryCache();
builder.Services.AddHostedService<SampleWorker>();

var host = builder.Build();
host.Run();
