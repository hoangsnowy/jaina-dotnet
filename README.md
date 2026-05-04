# Jaina .NET

A modular .NET 8 framework library providing production-ready abstractions and implementations for caching, data access, messaging, file storage, security, diagnostics, and notifications.

[![Build](https://img.shields.io/github/actions/workflow/status/HoangSnowy/jaina-dotnet/build.yml?branch=main)](https://github.com/HoangSnowy/jaina-dotnet/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

---

## Overview

Jaina follows a consistent pattern: each functional area has one **abstraction** package and one or more **provider** packages. You swap providers by changing a single DI registration — your application code never changes.

```
your app → Jaina abstraction (ICache, IFileStorage, IQueue<T>…)
                     ↓
           Jaina provider (Memory, Redis, Azure Blob, RabbitMQ…)
```

---

## Architecture

```
src/
  core/           Jaina.Core               Guard, Result<T>, extensions, HttpClientBase
  aspnetcore/     Jaina.AspNetCore         Problem Details, correlation ID, telemetry filters
  resilience/     Jaina.Resilience         Polly v8 named pipelines (retry/timeout/CB/hedging)
  servicediscovery/ Jaina.ServiceDiscovery  Microsoft.Extensions.ServiceDiscovery wrapper
  idempotency/    Jaina.Idempotency        IIdempotencyStore abstraction
                  Jaina.Idempotency.InMemory   IMemoryCache-backed store (dev/test)
                  Jaina.Idempotency.Redis      Distributed store (StackExchange.Redis)
                  Jaina.Idempotency.AspNetCore Middleware for Idempotency-Key replay
  caching/        Jaina.Caching            ICache abstraction
                  Jaina.Caching.Memory     In-process (Microsoft.Extensions.Caching.Memory)
                  Jaina.Caching.Redis      Distributed (StackExchange.Redis)
                  Jaina.Caching.Fusion     Multi-level (FusionCache)
  data/           Jaina.Data               IRepository<T>, IUnitOfWork abstractions
                  Jaina.Data.EfCore        EF Core provider (EfRepository, EfUnitOfWork)
                  Jaina.Data.Dapper        Dapper provider (DapperRepository)
                  Jaina.Data.Cqrs          Command/Query buses, domain events, event store
  messaging/      Jaina.Messaging          IQueue<T> / ITopic<T> abstraction
                  Jaina.Messaging.RabbitMQ RabbitMQ provider
                  Jaina.Messaging.AzureServiceBus  Azure Service Bus provider
                  Jaina.Messaging.Broadcast        In-memory broadcast (dev/test)
                  Jaina.Messaging.Outbox          Transactional outbox abstractions + relay
                  Jaina.Messaging.Outbox.InMemory In-memory outbox store (dev/test)
                  Jaina.Messaging.Outbox.EfCore   EF Core outbox (atomic with domain writes)
                  Jaina.Messaging.Inbox           Consumer-side dedup abstraction
                  Jaina.Messaging.Inbox.InMemory  In-memory inbox dedup (dev/test)
                  Jaina.Messaging.Inbox.Redis     Distributed dedup (Redis SETNX)
                  Jaina.Messaging.Inbox.EfCore    Distributed dedup (EF Core unique key)
                  Jaina.Messaging.Saga            Orchestration saga + reverse compensation
                  Jaina.Messaging.Saga.InMemory   In-memory saga state repository (dev/test)
                  Jaina.Messaging.Saga.EfCore     EF Core saga state repository
  storage/        Jaina.Storage            IFileStorage abstraction
                  Jaina.Storage.Local      File system
                  Jaina.Storage.AzureBlob  Azure Blob Storage
                  Jaina.Storage.AzureFileShare  Azure Files
                  Jaina.Storage.Sftp       SFTP
                  Jaina.Storage.Compression  ZIP utilities
  security/       Jaina.Security           AES, RSA, BCrypt, SHA, JWT helpers
                  Jaina.Security.Authentication  JWT bearer auth
                  Jaina.Security.Authentication.Client  Client credentials
                  Jaina.Security.KeyVault  Azure Key Vault
  observability/  Jaina.Observability      ITelemetry / ISpan abstraction + structured logging
                  Jaina.Observability.ApplicationInsights  Azure App Insights
                  Jaina.Observability.ElasticApm           Elastic APM
  mapping/        Jaina.Mapping            IMapper abstraction
                  Jaina.Mapping.Mapster    Mapster provider
  notifications/  Jaina.Notifications       IEmailSender, ISmsSender abstractions
                  Jaina.Notifications.Smtp         SMTP email provider (MailKit)
                  Jaina.Notifications.ConsoleSms   Console/logger SMS provider (dev/test)
samples/          Aspire AppHost, WebApi, Worker demos
tests/            xUnit test projects
docs/blog/        Cookbook posts (real-world patterns + error scenarios)
```

📚 **Cookbook** — see [`docs/blog/`](docs/blog/README.md) for runnable recipe posts:
- [Idempotency: surviving the mobile retry storm](docs/blog/2026-05-04-idempotency-retry-storm.md)
- [Outbox: never lose another order on Black Friday](docs/blog/2026-05-04-outbox-black-friday.md)
- More posts coming as M1+ modules ship

---

## Quick Start

### Prerequisites

- .NET 8 SDK or later

### Installation

Add packages from NuGet (replace providers as needed):

```bash
dotnet add package Jaina.Core
dotnet add package Jaina.AspNetCore       # Problem Details, correlation ID, filters
dotnet add package Jaina.Resilience       # Polly v8 named pipelines
dotnet add package Jaina.ServiceDiscovery # MS ServiceDiscovery wrapper
dotnet add package Jaina.Caching.Memory
dotnet add package Jaina.Data.EfCore     # or Jaina.Data.Dapper
dotnet add package Jaina.Mapping.Mapster
dotnet add package Jaina.Storage.Local
dotnet add package Jaina.Security
dotnet add package Jaina.Notifications
```

---

## Module Usage

### Core — Guard & Result\<T\>

```csharp
using Jaina.Core;
using Jaina.Core.Results;

// Guard clauses — use CallerArgumentExpression, no need to pass param name
public void Process(string input, object data)
{
    Guard.NotNullOrWhiteSpace(input);   // throws ArgumentException if null/whitespace
    Guard.NotNull(data);                // throws ArgumentNullException if null
    Guard.Requires<InvalidOperationException>(input.Length < 100, "Input too long");
}

// Result<T> — railway-oriented error handling
public Result<User> GetUser(int id)
{
    var user = _db.Find(id);
    if (user is null)
        return Result.Fail<User>($"User {id} not found");
    return Result.Ok(user);
}

// Consume
var result = GetUser(42);
if (result.IsSuccess)
    Console.WriteLine(result.Value!.Name);
else
    Console.WriteLine(result.Message);
```

---

### Caching

```csharp
// Program.cs — pick one provider
builder.Services.AddJainaMemoryCache();               // in-process
builder.Services.AddJainaRedisCache(o => {            // Redis
    o.ConnectionString = "localhost:6379";
});
builder.Services.AddJainaFusionCache();               // multi-level

// Usage
public class ProductService(ICache cache)
{
    public async Task<Product?> GetProductAsync(int id)
    {
        return await cache.GetAsync<Product>($"product:{id}",
            async () => await _db.FindAsync(id),
            TimeSpan.FromMinutes(10));
    }
}
```

---

### Data — Repository & Unit of Work

```csharp
// Program.cs
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(conn));
builder.Services.AddJainaUnitOfWork<AppDbContext>();   // from Jaina.Data.EfCore

// Entity
public class Order : IEntity
{
    public int Id { get; set; }
    public string Status { get; set; } = "";
}

// Repository — extend EfRepository<TContext, TEntity>
public class OrderRepository(AppDbContext ctx, IMapper mapper)
    : EfRepository<AppDbContext, Order>(ctx, mapper), IRepository<Order>;

// Usage
public class OrderService(IRepository<Order> repo, IUnitOfWork uow)
{
    public async Task CreateAsync(Order order)
    {
        await repo.CreateAsync(order);
        await uow.SaveChangesAsync();
    }

    public async Task<Order?> GetAsync(int id) =>
        await repo.GetEntityAsync(id);
}
```

---

### Data — CQRS Buses

```csharp
// Program.cs
builder.Services.AddJainaCqrs();
builder.Services.AddCommandHandler<CreateOrderCommand, CreateOrderCommandHandler>();
builder.Services.AddQueryHandler<GetOrderQuery, OrderDto, GetOrderQueryHandler>();

// Command
public record CreateOrderCommand(string CustomerName) : ICommand;

public class CreateOrderCommandHandler(IRepository<Order> repo, IUnitOfWork uow)
    : ICommandHandler<CreateOrderCommand>
{
    public async Task HandleAsync(CreateOrderCommand cmd, CancellationToken ct = default)
    {
        await repo.AddAsync(new Order { CustomerName = cmd.CustomerName });
        await uow.SaveChangesAsync(ct);
    }
}

// Query
public record GetOrderQuery(int Id) : IQuery<OrderDto?>;

public class GetOrderQueryHandler(IRepository<Order> repo)
    : IQueryHandler<GetOrderQuery, OrderDto?>
{
    public async Task<OrderDto?> HandleAsync(GetOrderQuery query, CancellationToken ct = default)
    {
        var order = await repo.GetByIdAsync(query.Id);
        return order is null ? null : new OrderDto(order.Id, order.CustomerName);
    }
}

// Dispatch
public class OrderController(ICommandBus commands, IQueryBus queries)
{
    public Task CreateOrder(string name) =>
        commands.SendAsync(new CreateOrderCommand(name));

    public Task<OrderDto?> GetOrder(int id) =>
        queries.SendAsync<GetOrderQuery, OrderDto?>(new GetOrderQuery(id));
}
```

---

### Messaging

```csharp
// Program.cs — pick one provider
builder.Services.AddJainaRabbitMQ(o => {
    o.HostName = "localhost";
    o.QueueName = "orders";
});

// Publish
public class OrderPublisher(IQueue<OrderCreatedEvent> queue)
{
    public Task PublishAsync(OrderCreatedEvent evt) =>
        queue.EnqueueAsync(evt);
}

// Subscribe
queue.Subscribe(async (msg, ct) =>
{
    Console.WriteLine($"Received: {msg.Body.OrderId}");
    await Task.CompletedTask;
});
```

---

### Storage

```csharp
// Program.cs — pick one provider
builder.Services.AddJainaLocalStorage(o => o.BasePath = "/data/uploads");
builder.Services.AddJainaAzureBlobStorage(o => {
    o.ConnectionString = "DefaultEndpointsProtocol=https;...";
    o.ContainerName = "uploads";
});

// Usage
public class FileService(IFileStorage storage)
{
    public async Task UploadAsync(string path, byte[] data)
    {
        await storage.SaveAsync(path, data);
    }

    public async Task<byte[]> DownloadAsync(string path)
    {
        if (!await storage.ExistsAsync(path))
            throw new FileNotFoundException(path);
        return await storage.GetBytesAsync(path);
    }

    public async Task<IEnumerable<string>> ListAsync(string directory) =>
        await storage.GetFileNamesAsync(directory);
}
```

---

### Security

```csharp
using Jaina.Security.Encryption;
using Jaina.Security.Hashing;
using Jaina.Security.Token;

// Password hashing (BCrypt)
string hash = BcryptHelper.Hash("myPassword");
bool valid = BcryptHelper.Verify("myPassword", hash);

// AES symmetric encryption
string cipher = AesHelper.Encrypt("sensitive data", pepper: "app-key", salt: "user-salt");
string plain  = AesHelper.Decrypt(cipher, pepper: "app-key", salt: "user-salt");

// SHA-256
string digest = Sha256Helper.Hash("data");

// JWT
var token = new JwtSecurityToken(...);
var read  = JwtHelper.ReadToken(tokenString);

// JWT Bearer auth
builder.Services.AddJainaJwtAuthentication(o => {
    o.SecretKey = "your-secret";
    o.Issuer    = "your-api";
    o.Audience  = "your-clients";
});
```

---

### Resilience

```csharp
// Register the four default Jaina pipelines (default / queue-publish / external-http / database)
builder.Services.AddJainaResilience();

// Or customize / add your own
builder.Services.AddJainaResilience(b => b.AddPipeline("retry-fast", p => p
    .AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 2, Delay = TimeSpan.FromMilliseconds(50) })
    .AddTimeout(TimeSpan.FromSeconds(2))));

// Resolve and execute
public class OrderClient(ResiliencePipelineProvider<string> pipelines)
{
    public async Task<Result> PlaceOrderAsync(Order order, CancellationToken ct)
    {
        var pipeline = pipelines.GetPipeline(JainaResiliencePipelines.ExternalHttp);
        return await pipeline.ExecuteAsync(async token =>
            await _http.PostAsync("/orders", order, token), ct);
    }
}
```

---

### Observability

```csharp
// Program.cs — pick one provider
builder.Services.AddJainaApplicationInsights();
builder.Services.AddJainaElasticApm();

// Correlation ID middleware
app.UseCorrelationId();

// Usage
public class PaymentService(ITelemetry telemetry)
{
    public async Task<Result> ChargeAsync(decimal amount)
    {
        using var span = telemetry.StartSpan("PaymentService.Charge", TelemetryTypes.External);
        span.SetLabel("amount", amount.ToString());
        try
        {
            await _gateway.ChargeAsync(amount);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            span.CaptureException(ex);
            return Result.Fail(ex);
        }
    }
}
```

---

### Mapping

```csharp
// Program.cs
builder.Services.AddJainaMapster();                    // default — no custom rules
builder.Services.AddJainaMapster(cfg => {              // with custom rules
    cfg.NewConfig<User, UserDto>()
       .Map(dest => dest.FullName, src => $"{src.FirstName} {src.LastName}");
});

// Inject and use
public class UserService(IMapper mapper)
{
    public UserDto ToDto(User user) => mapper.Map<UserDto>(user);
    public UserDto ToDtoTyped(User user) => mapper.Map<User, UserDto>(user);
}
```

---

### Notifications

```csharp
// Program.cs — pick providers independently
builder.Services.AddJainaSmtpEmail(o => {   // from Jaina.Notifications.Smtp
    o.Host     = "smtp.example.com";
    o.Port     = 587;
    o.Username = "user@example.com";
    o.Password = "secret";
});
builder.Services.AddJainaConsoleSms();       // from Jaina.Notifications.ConsoleSms — dev/test

// Email
public class WelcomeService(IEmailSender email)
{
    public Task SendWelcomeAsync(string to) =>
        email.SendAsync(new EmailMessage
        {
            To      = [to],
            Subject = "Welcome!",
            Body    = "<h1>Welcome to Jaina</h1>",
            IsHtml  = true
        });
}

// SMS
public class AlertService(ISmsSender sms)
{
    public Task SendAlertAsync(string phone, string message) =>
        sms.SendAsync(new SmsMessage
        {
            From = "+15550001234",
            To   = phone,
            Body = message
        });
}
```

---

## Running the Samples

The samples use [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/) to orchestrate Redis, SQL Server, and the application services.

```bash
# Clone and restore
git clone https://github.com/HoangSnowy/jaina-dotnet.git
cd jaina-dotnet
dotnet restore Jaina.sln

# Run Aspire AppHost (starts everything)
dotnet run --project samples/Jaina.Samples.AppHost

# Or run the Web API directly
dotnet run --project samples/Jaina.Samples.WebApi
# Open: http://localhost:5000/swagger
```

### Available Sample Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/cache/{key}` | Read from cache |
| POST | `/api/cache/{key}` | Write to cache |
| DELETE | `/api/cache/{key}` | Invalidate cache entry |
| POST | `/api/files/{*path}` | Upload a file |
| GET | `/api/files/{*path}` | Download a file |
| GET | `/api/files` | List files |
| POST | `/api/items` | Create item (CQRS command) |
| GET | `/api/items/{id}` | Get item (CQRS query) |
| POST | `/api/security/hash` | Hash a password (BCrypt) |
| POST | `/api/security/verify` | Verify a password |
| POST | `/api/security/encrypt` | AES encrypt |
| POST | `/api/security/decrypt` | AES decrypt |
| POST | `/api/notify/sms` | Send SMS (logged to console) |

---

## Running Tests

```bash
dotnet test Jaina.sln

# Single project
dotnet test tests/Jaina.Core.Tests/Jaina.Core.Tests.csproj

# Single test class
dotnet test --filter "FullyQualifiedName~GuardTests"
```

---

## Package Versioning

All NuGet package versions are centralized in [`Directory.Packages.props`](Directory.Packages.props). Do **not** specify versions in individual `.csproj` files.

---

## Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feat/your-feature`
3. Follow the code style defined in [`.editorconfig`](.editorconfig)
4. Ensure `dotnet build Jaina.sln` passes with zero warnings
5. Add or update tests for your changes
6. Open a pull request

---

## License

MIT — see [LICENSE](LICENSE) for details.
