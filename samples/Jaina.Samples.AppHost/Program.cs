var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("cache");
var sql = builder.AddSqlServer("sql").AddDatabase("sampledb");

// Note: RabbitMQ hosting removed from Aspire due to RabbitMQ.Client 7.x incompatibility
// with Aspire.Hosting.RabbitMQ (requires 6.x). Run RabbitMQ externally or via docker-compose.

builder.AddProject<Projects.Jaina_Samples_WebApi>("webapi")
    .WithReference(redis)
    .WithReference(sql);

builder.AddProject<Projects.Jaina_Samples_Worker>("worker")
    .WithReference(redis);

builder.Build().Run();
