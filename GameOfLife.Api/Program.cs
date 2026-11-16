using System.Text.Json.Serialization;
using GameOfLife.Api.ErrorHandling;
using GameOfLife.Core.Abstractions;
using GameOfLife.Core.Configuration;
using GameOfLife.Core.Engine;
using GameOfLife.Core.Simulation;
using GameOfLife.Infrastructure;
using GameOfLife.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services
    .AddOptions<GameSettings>()
    .Bind(builder.Configuration.GetSection(GameSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

string connectionString = builder.Configuration.GetConnectionString("GameOfLife")
    ?? throw new InvalidOperationException("Connection string 'GameOfLife' is not configured.");

builder.Services.AddInfrastructure(connectionString);

// The engine and simulation service are stateless and allocation-free across requests.
builder.Services.AddSingleton<IGameOfLifeEngine, FiniteGridEngine>();
builder.Services.AddSingleton<ISimulationService, SimulationService>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

// SQLite needs no external setup: create the schema on first start so the API is
// crash/restart safe out of the box. (Production systems would apply EF migrations here.)
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<GameOfLifeDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();

/// <summary>Exposed for WebApplicationFactory-based integration tests.</summary>
public partial class Program;
