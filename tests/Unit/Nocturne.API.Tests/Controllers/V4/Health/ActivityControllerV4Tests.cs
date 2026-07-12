using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Nocturne.API.Controllers.V4.Health;
using Nocturne.API.Models.Requests.V4;
using Nocturne.Core.Contracts.Health;
using Nocturne.Core.Contracts.V4;
using Nocturne.Core.Models;
using Nocturne.Core.Models.Authorization;
using Xunit;

namespace Nocturne.API.Tests.Controllers.V4.Health;

/// <summary>
/// Controller-level tests for the per-category write-scope gate on the v4 merged activity endpoint.
/// </summary>
[Trait("Category", "Unit")]
public class ActivityControllerV4Tests
{
    private readonly Mock<IActivityService> _service = new();
    private readonly Mock<IActivityDecomposer> _decomposer = new();
    private readonly ActivityController _controller;

    public ActivityControllerV4Tests()
    {
        _controller = new ActivityController(_service.Object, _decomposer.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }

    private void GrantScopes(params string[] scopes) =>
        _controller.HttpContext.Items["GrantedScopes"] = (IReadOnlySet<string>)new HashSet<string>(scopes);

    [Fact]
    public async Task CreateActivities_SleepRecordWithoutSleepScope_ReturnsForbidden()
    {
        GrantScopes(OAuthScopes.GlucoseReadWrite);
        _decomposer.Setup(d => d.RequiredWriteScope(It.IsAny<Activity>())).Returns(OAuthScopes.SleepReadWrite);

        var result = await _controller.CreateActivities(
            [new UpsertActivityRequest { Type = "sleep", Mills = 1700000000000 }], CancellationToken.None);

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _service.Verify(
            x => x.CreateActivitiesAsync(It.IsAny<IEnumerable<Activity>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeleteActivity_ExistingRecordIsSleep_WithoutSleepScope_ReturnsForbidden()
    {
        const string id = "sleep-session-1";
        GrantScopes(OAuthScopes.GlucoseReadWrite);
        _service.Setup(x => x.GetActivityByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Activity { Id = id, Type = "sleep" });
        _decomposer.Setup(d => d.RequiredWriteScope(It.IsAny<Activity>())).Returns(OAuthScopes.SleepReadWrite);

        var result = await _controller.DeleteActivity(id, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _service.Verify(x => x.DeleteActivityAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteActivity_ExistingRecordIsSleep_WithSleepScope_Proceeds()
    {
        const string id = "sleep-session-1";
        GrantScopes(OAuthScopes.SleepReadWrite);
        _service.Setup(x => x.GetActivityByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Activity { Id = id, Type = "sleep" });
        _decomposer.Setup(d => d.RequiredWriteScope(It.IsAny<Activity>())).Returns(OAuthScopes.SleepReadWrite);
        _service.Setup(x => x.DeleteActivityAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _controller.DeleteActivity(id, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        _service.Verify(x => x.DeleteActivityAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
