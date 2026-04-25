using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Nocturne.API.Controllers.V1;
using Nocturne.Core.Contracts.Entries;
using Nocturne.Core.Contracts.Repositories;
using Xunit;

namespace Nocturne.API.Tests.Controllers.V1;

/// <summary>
/// Unit tests for CountController verifying IEntryStore integration.
/// </summary>
[Trait("Category", "Unit")]
public class CountControllerTests
{
    private readonly Mock<IEntryStore> _mockEntryStore;
    private readonly Mock<ITreatmentRepository> _mockTreatmentRepository;
    private readonly Mock<IDeviceStatusRepository> _mockDeviceStatusRepository;
    private readonly Mock<IProfileRepository> _mockProfileRepository;
    private readonly Mock<IFoodRepository> _mockFoodRepository;
    private readonly Mock<IActivityRepository> _mockActivityRepository;
    private readonly Mock<ILogger<CountController>> _mockLogger;
    private readonly CountController _controller;

    public CountControllerTests()
    {
        _mockEntryStore = new Mock<IEntryStore>();
        _mockTreatmentRepository = new Mock<ITreatmentRepository>();
        _mockDeviceStatusRepository = new Mock<IDeviceStatusRepository>();
        _mockProfileRepository = new Mock<IProfileRepository>();
        _mockFoodRepository = new Mock<IFoodRepository>();
        _mockActivityRepository = new Mock<IActivityRepository>();
        _mockLogger = new Mock<ILogger<CountController>>();

        _controller = new CountController(
            _mockEntryStore.Object,
            _mockTreatmentRepository.Object,
            _mockDeviceStatusRepository.Object,
            _mockProfileRepository.Object,
            _mockFoodRepository.Object,
            _mockActivityRepository.Object,
            _mockLogger.Object
        );

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
    }

    [Fact]
    public async Task CountEntries_DelegatesToEntryStore()
    {
        // Arrange
        var find = "{\"type\":\"sgv\"}";
        var type = "sgv";
        _mockEntryStore
            .Setup(s => s.CountAsync(find, type, It.IsAny<CancellationToken>()))
            .ReturnsAsync(42L);

        // Act
        var result = await _controller.CountEntries(find, type);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<CountResponse>().Subject;
        response.Count.Should().Be(42L);

        _mockEntryStore.Verify(
            s => s.CountAsync(find, type, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task CountGeneric_Entries_DelegatesToEntryStore()
    {
        // Arrange
        var find = "{\"dateString\":{\"$gte\":\"2024-01-01\"}}";
        var type = "mbg";
        _mockEntryStore
            .Setup(s => s.CountAsync(find, type, It.IsAny<CancellationToken>()))
            .ReturnsAsync(7L);

        // Act
        var result = await _controller.CountGeneric("entries", find, type);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<CountResponse>().Subject;
        response.Count.Should().Be(7L);

        _mockEntryStore.Verify(
            s => s.CountAsync(find, type, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
}
