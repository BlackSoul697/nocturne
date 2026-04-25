using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;

namespace Nocturne.Infrastructure.Data.Tests.Integration;

/// <summary>
/// Integration tests for PostgreSQL repositories using real PostgreSQL database
/// These tests verify that MongoDB-style queries work correctly with PostgreSQL
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "PostgreSQL")]
[Trait("Category", "Repository")]
public class PostgreSqlRepositoryIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;
    private ServiceProvider? _serviceProvider;
    private NocturneDbContext? _dbContext;

    public async Task InitializeAsync()
    {
        // Create and start PostgreSQL container
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:15-alpine")
            .WithDatabase("nocturne_test")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .WithCleanUp(true)
            .Build();

        await _postgresContainer.StartAsync();

        // Setup services
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        services.AddDbContext<NocturneDbContext>(options =>
            options
                .UseNpgsql(_postgresContainer.GetConnectionString())
                .EnableSensitiveDataLogging()
                .EnableDetailedErrors()
        );

        services.AddScoped<TreatmentRepository>();

        _serviceProvider = services.BuildServiceProvider();

        // Create database schema
        _dbContext = _serviceProvider.GetRequiredService<NocturneDbContext>();
        await _dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_serviceProvider != null)
        {
            await _serviceProvider.DisposeAsync();
        }

        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }
    }

    #region Treatment Repository Integration Tests

    [Fact]
    public async Task TreatmentRepository_ShouldPersistAndRetrieveData_WithPostgreSQL()
    {
        // Arrange
        using var scope = _serviceProvider!.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<TreatmentRepository>();

        var testTreatments = new[]
        {
            CreateTestTreatment(insulin: 3.5, eventType: "Meal Bolus", carbs: 45.0),
            CreateTestTreatment(insulin: 1.5, eventType: "Correction Bolus"),
            CreateTestTreatment(carbs: 15.0, eventType: "Carb Correction"),
        };

        // Act - Create
        var createdTreatments = await repository.CreateTreatmentsAsync(testTreatments);

        // Act - Retrieve
        var allTreatments = await repository.GetTreatmentsAsync(count: 10);
        var mealBoluses = await repository.GetTreatmentsAsync(eventType: "Meal Bolus", count: 10);
        var count = await repository.CountTreatmentsAsync();

        // Assert
        createdTreatments.Should().HaveCount(3);
        allTreatments.Should().HaveCount(3);
        mealBoluses.Should().HaveCount(1);
        count.Should().Be(3);

        // Verify data integrity
        var mealBolus = mealBoluses.First();
        mealBolus.Insulin.Should().Be(3.5);
        mealBolus.Carbs.Should().Be(45.0);
        mealBolus.EventType.Should().Be("Meal Bolus");
    }

    [Fact]
    public async Task TreatmentRepository_ShouldHandleComplexTreatmentData_WithPostgreSQL()
    {
        // Arrange
        using var scope = _serviceProvider!.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<TreatmentRepository>();

        var complexTreatment = CreateTestTreatment();
        complexTreatment.Insulin = 4.25;
        complexTreatment.Carbs = 62.5;
        complexTreatment.Protein = 18.0;
        complexTreatment.Fat = 12.5;
        complexTreatment.Duration = 180.0;
        complexTreatment.Percent = 85.0;
        complexTreatment.Notes = "Complex meal with high fat content";
        complexTreatment.BolusCalc = new Dictionary<string, object>
        {
            ["carbs"] = 60,
            ["cob"] = 8.5,
            ["iob"] = 0.8,
            ["ic"] = 15.0,
            ["isf"] = 50.0,
        };
        complexTreatment.AbsorptionTime = 240;
        complexTreatment.SplitNow = 60.0;
        complexTreatment.SplitExt = 40.0;

        // Act
        var result = await repository.CreateTreatmentsAsync(new[] { complexTreatment });
        var retrieved = await repository.GetTreatmentByIdAsync(complexTreatment.Id!);

        // Assert
        result.Should().HaveCount(1);
        retrieved.Should().NotBeNull();

        retrieved!.Insulin.Should().Be(4.25);
        retrieved.Carbs.Should().Be(62.5);
        retrieved.Protein.Should().Be(18.0);
        retrieved.Fat.Should().Be(12.5);
        retrieved.Duration.Should().Be(180.0);
        retrieved.AbsorptionTime.Should().Be(240);
        retrieved.BolusCalc.Should().NotBeNull();
        retrieved.Notes.Should().Be("Complex meal with high fat content");
    }

    #endregion

    #region MongoDB Query Compatibility Tests

    [Fact]
    public async Task Repositories_ShouldLogMongoDBQueries_ForFutureImplementation()
    {
        // Arrange
        using var scope = _serviceProvider!.CreateScope();
        var treatmentRepository = scope.ServiceProvider.GetRequiredService<TreatmentRepository>();

        var testTreatment = CreateTestTreatment(insulin: 2.5, eventType: "Meal Bolus");

        await treatmentRepository.CreateTreatmentsAsync(new[] { testTreatment });

        // Act - These should not throw even though MongoDB query parsing is not implemented
        var treatmentResult = await treatmentRepository.GetTreatmentsWithAdvancedFilterAsync(
            findQuery: "{\"eventType\":\"Meal Bolus\",\"insulin\":{\"$gte\":2.0}}"
        );

        // Assert - Should return data even without query parsing
        treatmentResult.Should().HaveCount(1);
        treatmentResult.First().Insulin.Should().Be(2.5);
    }

    #endregion

    #region Performance and Stress Tests

    [Fact]
    [Trait("Category", "Performance")]
    public async Task Repositories_ShouldMaintainPerformance_WithLargeDatasets()
    {
        // Arrange
        using var scope = _serviceProvider!.CreateScope();
        var treatmentRepository = scope.ServiceProvider.GetRequiredService<TreatmentRepository>();

        const int treatmentCount = 500;

        var treatments = Enumerable
            .Range(1, treatmentCount)
            .Select(i =>
                CreateTestTreatment(
                    insulin: 0.5 + (i % 20) * 0.25, // Vary insulin from 0.5-5.0
                    eventType: i % 3 == 0 ? "Correction Bolus" : "Meal Bolus",
                    mills: DateTimeOffset.UtcNow.AddMinutes(-i * 2).ToUnixTimeMilliseconds()
                )
            )
            .ToArray();

        // Act - Bulk operations with timing
        var insertStart = DateTimeOffset.UtcNow;

        await treatmentRepository.CreateTreatmentsAsync(treatments);

        var insertDuration = DateTimeOffset.UtcNow - insertStart;

        // Act - Query operations with timing
        var queryStart = DateTimeOffset.UtcNow;

        var mealBoluses = await treatmentRepository.GetTreatmentsAsync(
            eventType: "Meal Bolus",
            count: 200
        );
        var totalTreatmentsCount = await treatmentRepository.CountTreatmentsAsync();

        var queryDuration = DateTimeOffset.UtcNow - queryStart;

        // Assert - Data integrity
        totalTreatmentsCount.Should().Be(treatmentCount);
        mealBoluses.Should().HaveCount(200);

        mealBoluses.All(t => t.EventType == "Meal Bolus").Should().BeTrue();

        // Assert - Performance thresholds
        insertDuration
            .Should()
            .BeLessThan(TimeSpan.FromSeconds(30), "Bulk insert should complete within 30 seconds");
        queryDuration
            .Should()
            .BeLessThan(
                TimeSpan.FromSeconds(10),
                "Complex queries should complete within 10 seconds"
            );
    }

    #endregion

    #region Test Helper Methods

    private static Treatment CreateTestTreatment(
        double? insulin = 2.0,
        string eventType = "Correction Bolus",
        long? mills = null,
        double? carbs = null
    )
    {
        var timestamp = mills ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return new Treatment
        {
            Id = Guid.NewGuid().ToString(),
            Mills = timestamp,
            Created_at = DateTimeOffset
                .FromUnixTimeMilliseconds(timestamp)
                .ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            EventType = eventType,
            Insulin = insulin,
            Carbs = carbs,
            Notes = $"Test treatment {Guid.NewGuid().ToString()[..8]}",
            EnteredBy = "test-user",
        };
    }

    #endregion
}
