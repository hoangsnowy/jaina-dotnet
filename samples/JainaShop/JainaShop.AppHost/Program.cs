var builder = DistributedApplication.CreateBuilder(args);

// Shared infra — Aspire spins up Redis once and injects the connection string into
// every dependent service. Postgres / RabbitMQ are not wired here yet because the
// in-memory providers cover the demo; swap when you're ready for production-shaped tests.
var redis = builder.AddRedis("cache");

var identity = builder.AddProject<Projects.JainaShop_Identity>("identity");

var catalog = builder.AddProject<Projects.JainaShop_Catalog>("catalog");

var orders = builder.AddProject<Projects.JainaShop_Orders>("orders");

var notifier = builder.AddProject<Projects.JainaShop_Notifier>("notifier")
    .WithReference(redis);

builder.AddProject<Projects.JainaShop_Gateway>("gateway")
    .WithReference(catalog)
    .WithReference(orders)
    .WithReference(identity);

builder.Build().Run();
