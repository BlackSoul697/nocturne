using FluentAssertions;
using Nocturne.API.Services;

namespace Nocturne.API.Tests.Services;

public class PlatformSettingsServiceTests
{
    [Fact]
    public void A_telegram_bot_cannot_be_enabled_without_its_webhook_secret_token()
    {
        PlatformSettingsService.GetSchemas()["telegram"]
            .Should().ContainSingle(f => f.Name == "webhookSecretToken")
            .Which.Required.Should().BeTrue();
    }
}
