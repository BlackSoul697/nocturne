using System.Data.Common;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nocturne.Core.Contracts.Infrastructure;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Repositories;
using Nocturne.Infrastructure.Data.Services;
using Xunit;

#pragma warning disable CA1515 // Consider making public types internal

namespace Nocturne.Infrastructure.Data.Performance.Tests;

/// <summary>
/// Performance benchmarks for Treatment repository
/// Tests various scenarios including bulk operations, complex queries, and edge cases
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[Trait("Category", "Performance")]
[Trait("Category", "BenchmarkDotNet")]
public class RepositoryPerformanceBenchmarks : IDisposable
{
    private ServiceProvider? _serviceProvider;
    private NocturneDbContext? _dbContext;
    private DbConnection? _connection;
    private TreatmentRepository? _treatmentRepository;

    [GlobalSetup]
    public void Setup()
    {
        // Create in-memory SQLite database for benchmarking
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));

        services.AddDbContext<NocturneDbContext>(options =>
            options
                .UseSqlite(_connection)
                .EnableSensitiveDataLogging(false) // Disable for performance
                .EnableDetailedErrors(false)
        );

        services.AddScoped<IQueryParser, QueryParser>();
        services.AddScoped<TreatmentRepository>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<NocturneDbContext>();

        // Create database schema
        _dbContext.Database.EnsureCreated();

        // Initialize repositories
        _treatmentRepository = _serviceProvider.GetRequiredService<TreatmentRepository>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _serviceProvider?.Dispose();
        _connection?.Dispose();
    }

    #region Treatment Repository Benchmarks

    [Benchmark]
    [Arguments(100)]
    [Arguments(500)]
    [Arguments(1000)]
    public async Task CreateTreatments_BulkInsert(int treatmentCount)
    {
        var treatments = GenerateTestTreatments(treatmentCount);
        await _treatmentRepository!.CreateTreatmentsAsync(treatments);
    }

    [Benchmark]
    [Arguments(10)]
    [Arguments(50)]
    [Arguments(100)]
    public async Task QueryTreatments_WithPagination(int pageSize)
    {
        // Setup
        if (await _treatmentRepository!.CountTreatmentsAsync() < pageSize * 2)
        {
            var treatments = GenerateTestTreatments(pageSize * 5);
            await _treatmentRepository.CreateTreatmentsAsync(treatments);
        }

        // Benchmark
        var result = await _treatmentRepository.GetTreatmentsAsync(count: pageSize, skip: 0);
        _ = result.ToList();
    }

    [Benchmark]
    public async Task QueryTreatments_WithEventTypeFilter()
    {
        // Setup
        if (await _treatmentRepository!.CountTreatmentsAsync() < 100)
        {
            var treatments = GenerateTestTreatments(200);
            await _treatmentRepository.CreateTreatmentsAsync(treatments);
        }

        // Benchmark
        var result = await _treatmentRepository.GetTreatmentsAsync(
            eventType: "Meal Bolus",
            count: 50
        );
        _ = result.ToList();
    }

    [Benchmark]
    public async Task QueryTreatments_WithAdvancedFilter()
    {
        // Setup
        if (await _treatmentRepository!.CountTreatmentsAsync() < 100)
        {
            var treatments = GenerateTestTreatments(200);
            await _treatmentRepository.CreateTreatmentsAsync(treatments);
        }

        // Benchmark
        var filterTime = DateTimeOffset.UtcNow.AddHours(-12);
        var result = await _treatmentRepository.GetTreatmentsWithAdvancedFilterAsync(
            dateString: filterTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            count: 50
        );
        _ = result.ToList();
    }

    #endregion

    #region Helper Methods

    private static Treatment[] GenerateTestTreatments(int count)
    {
        var random = new Random(42); // Fixed seed for consistent benchmarks
        var baseTime = DateTimeOffset.UtcNow;
        var eventTypes = new[]
        {
            "Meal Bolus",
            "Correction Bolus",
            "Carb Correction",
            "BG Check",
            "Temp Basal",
        };

        return Enumerable
            .Range(1, count)
            .Select(i => new Treatment
            {
                Id = Guid.NewGuid().ToString(),
                Mills = baseTime.AddMinutes(-i * 2).ToUnixTimeMilliseconds(),
                Created_at = baseTime.AddMinutes(-i * 2).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                EventType = eventTypes[i % eventTypes.Length],
                Insulin = i % 3 == 0 ? null : 0.5 + random.NextDouble() * 5, // Random insulin 0.5-5.5
                Carbs = i % 2 == 0 ? null : random.Next(5, 101), // Random carbs 5-100
                Notes = $"Test treatment {i}",
                EnteredBy = $"user-{i % 3}" // 3 different users
            })
            .ToArray();
    }

    #endregion

    public void Dispose()
    {
        _serviceProvider?.Dispose();
        _connection?.Dispose();
    }
}
