using Microsoft.Extensions.DependencyInjection;
using Nocturne.API.Services.Treatments;
using Nocturne.Core.Contracts.Notifications;
using Nocturne.Core.Contracts.Connectors;
using Nocturne.Core.Models;

namespace Nocturne.API.Services.NotificationActionHandlers;

/// <summary>
/// Handles user actions (accept/reject/dismiss) on <c>meal_matching.suggested_match</c>
/// in-app notifications produced by <see cref="MealMatchingService"/>. Accept is deferred to
/// <c>MealMatchingController</c>; reject and dismiss are handled here by archiving the notification.
/// </summary>
/// <seealso cref="INotificationActionHandler"/>
public class MealMatchActionHandler(
    IServiceProvider serviceProvider,
    IConnectorFoodEntryRepository foodEntryRepository,
    ILogger<MealMatchActionHandler> logger
) : INotificationActionHandler
{
    public string NotificationType => "meal_matching.suggested_match";

    public async Task<bool> HandleAsync(
        Guid notificationId,
        string actionId,
        string userId,
        string? sourceId,
        Dictionary<string, object>? metadata,
        CancellationToken cancellationToken = default)
    {
        switch (actionId.ToLowerInvariant())
        {
            case "accept":
                // Accept action is handled via MealMatchingController
                // Just archive the notification here
                var notificationService = serviceProvider.GetRequiredService<IInAppNotificationService>();
                return await notificationService.ArchiveNotificationAsync(
                    notificationId,
                    NotificationArchiveReason.Completed,
                    cancellationToken);

            case "dismiss":
                if (sourceId != null && Guid.TryParse(sourceId, out var foodEntryId))
                {
                    // Mark the food entry as standalone
                    await foodEntryRepository.UpdateStatusAsync(
                        foodEntryId,
                        ConnectorFoodEntryStatus.Standalone,
                        cancellationToken);
                }
                var dismissNotificationService = serviceProvider.GetRequiredService<IInAppNotificationService>();
                return await dismissNotificationService.ArchiveNotificationAsync(
                    notificationId,
                    NotificationArchiveReason.Dismissed,
                    cancellationToken);

            case "review":
                // Review opens a dialog client-side, just return true
                return true;

            default:
                logger.LogWarning(
                    "Unknown action {ActionId} for meal match notification {NotificationId}",
                    actionId, notificationId);
                return false;
        }
    }
}
