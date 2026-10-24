using Nocturne.Core.Contracts.Notifications;
using Nocturne.Core.Contracts.Identity;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models;
using Nocturne.API.Services.Realtime;

namespace Nocturne.API.Services.Notifications;

/// <summary>
/// In-memory alarm and admin-notification service with 1:1 legacy JavaScript compatibility.
/// Implements functionality from <c>notifications.js</c> and <c>adminnotifies.js</c>. Alarm snoozes
/// live in <see cref="LegacyAlarmSnoozeStore"/>, Pushover receipts and admin notifications in
/// <see cref="LegacyNotificationStore"/>. Also bridges legacy Pushover receipt polling.
/// </summary>
/// <seealso cref="INotificationV1Service"/>
public class NotificationV1Service : INotificationV1Service
{
    private readonly ILogger<NotificationV1Service> _logger;
    private readonly IAuthorizationService _authorizationService;
    private readonly ISignalRBroadcastService _signalRBroadcastService;
    private readonly ITenantAccessor _tenantAccessor;
    private readonly LegacyAlarmSnoozeStore _snoozes;
    private readonly LegacyNotificationStore _notifications;
    private readonly IPushoverService? _pushoverService;

    public NotificationV1Service(
        ILogger<NotificationV1Service> logger,
        IAuthorizationService authorizationService,
        ISignalRBroadcastService signalRBroadcastService,
        ITenantAccessor tenantAccessor,
        LegacyAlarmSnoozeStore snoozes,
        LegacyNotificationStore notifications,
        IPushoverService? pushoverService = null
    )
    {
        _logger = logger;
        _authorizationService = authorizationService;
        _signalRBroadcastService = signalRBroadcastService;
        _tenantAccessor = tenantAccessor;
        _snoozes = snoozes;
        _notifications = notifications;
        _pushoverService = pushoverService;
    }

    public async Task<NotificationAckResponse> AckNotificationAsync(
        NotificationAckRequest request,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogDebug(
                "Processing notification acknowledgment for level {Level}, group {Group}",
                request.Level,
                request.Group
            );

            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var silenceTime = request.Time ?? NotificationAckRequest.DefaultSilenceTimeMs;

            if (!_snoozes.TrySnooze(_tenantAccessor.TenantId, request.Level, request.Group, silenceTime))
            {
                var message =
                    $"Alarm has already been snoozed, don't snooze it again, level: {request.Level}, group: {request.Group}";
                _logger.LogWarning(message);
                return new NotificationAckResponse
                {
                    Success = false,
                    Message = message,
                    Timestamp = currentTime,
                };
            }

            _logger.LogInformation(
                "Acknowledged alarm - level: {Level}, group: {Group}, silenceTime: {SilenceTime}ms",
                request.Level,
                request.Group,
                silenceTime
            );

            // Send clear alarm notification if requested (replaces legacy ctx.bus.emit('notification', notify))
            if (request.SendClear == true)
            {
                var clearNotification = new NotificationBase
                {
                    Clear = true,
                    Group = request.Group,
                    Level = request.Level,
                    Title = $"Alarm Cleared - {request.Group}",
                    Message = $"Level {request.Level} alarm has been acknowledged and cleared",
                    Timestamp = currentTime,
                };

                try
                {
                    await _signalRBroadcastService.BroadcastClearAlarmAsync(clearNotification);
                    _logger.LogDebug(
                        "Broadcasted clear alarm event for level {Level}, group {Group}",
                        request.Level,
                        request.Group
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to broadcast clear alarm event for level {Level}, group {Group}",
                        request.Level,
                        request.Group
                    );
                }
            }

            // If level 2 (URGENT), also acknowledge level 1 (WARN) - legacy behavior
            if (request.Level == 2)
            {
                await AckNotificationAsync(
                    new NotificationAckRequest
                    {
                        Level = 1,
                        Group = request.Group,
                        Time = request.Time,
                    },
                    cancellationToken
                );
            }

            return new NotificationAckResponse
            {
                Success = true,
                Message = $"Acknowledged {request.Group} - Level {request.Level}",
                Timestamp = currentTime,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error acknowledging notification for level {Level}, group {Group}",
                request.Level,
                request.Group
            );
            return new NotificationAckResponse
            {
                Success = false,
                Message = "Internal error processing acknowledgment",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
        }
    }

    public async Task<NotificationAckResponse> ProcessPushoverCallbackAsync(
        PushoverCallbackRequest request,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogDebug(
                "Processing Pushover callback - receipt: {Receipt}, status: {Status}",
                request.Receipt,
                request.Status
            );

            // Validate request
            if (string.IsNullOrEmpty(request.Receipt))
            {
                return new NotificationAckResponse
                {
                    Success = false,
                    Message = "Receipt is required for Pushover callback",
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                };
            }

            // Handle acknowledged notifications (status = 1)
            if (request.Status == 1 && request.AcknowledgedAt.HasValue)
            {
                _logger.LogInformation(
                    "Pushover notification acknowledged - receipt: {Receipt}, acknowledged_by: {AcknowledgedBy}",
                    request.Receipt,
                    request.AcknowledgedBy
                );

                // Find the corresponding alarm from the receipt mapping
                if (_notifications.TryGetReceipt(_tenantAccessor.TenantId, request.Receipt, out var receiptInfo))
                {
                    _logger.LogDebug(
                        "Found alarm mapping for receipt {Receipt}: level {Level}, group {Group}",
                        request.Receipt,
                        receiptInfo.Level,
                        receiptInfo.Group
                    );

                    // Acknowledge the corresponding alarm
                    var ackRequest = new NotificationAckRequest
                    {
                        Level = receiptInfo.Level,
                        Group = receiptInfo.Group,
                        Time = NotificationAckRequest.DefaultSilenceTimeMs,
                        SendClear = true,
                    };

                    // Process the acknowledgment using existing logic
                    var ackResult = await AckNotificationAsync(ackRequest, cancellationToken);

                    if (ackResult.Success)
                    {
                        // Clean up the receipt mapping as it's no longer needed
                        _notifications.RemoveReceipt(_tenantAccessor.TenantId, request.Receipt);

                        _logger.LogInformation(
                            "Successfully acknowledged alarm via Pushover - level: {Level}, group: {Group}",
                            receiptInfo.Level,
                            receiptInfo.Group
                        );
                    }

                    return new NotificationAckResponse
                    {
                        Success = ackResult.Success,
                        Message = ackResult.Success
                            ? "Pushover callback processed successfully"
                            : $"Pushover callback received but acknowledgment failed: {ackResult.Message}",
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    };
                }
                else
                {
                    _logger.LogWarning(
                        "No alarm mapping found for Pushover receipt: {Receipt}",
                        request.Receipt
                    );
                    return new NotificationAckResponse
                    {
                        Success = true,
                        Message = "Pushover callback processed successfully",
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    };
                }
            }

            // Handle expired notifications (status = 0)
            if (request.Status == 0)
            {
                _logger.LogDebug(
                    "Pushover notification expired - receipt: {Receipt}",
                    request.Receipt
                );

                // Clean up expired receipt mapping
                _notifications.RemoveReceipt(_tenantAccessor.TenantId, request.Receipt);

                return new NotificationAckResponse
                {
                    Success = true,
                    Message = "Pushover callback received but no action taken",
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                };
            }

            // Handle other status codes
            return new NotificationAckResponse
            {
                Success = true,
                Message =
                    $"Pushover callback received with status {request.Status}, no action taken",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing Pushover callback for receipt {Receipt}",
                request.Receipt
            );
            return new NotificationAckResponse
            {
                Success = false,
                Message = "Internal error processing Pushover callback",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
        }
    }

    public async Task<AdminNotifiesResponse> GetAdminNotifiesAsync(
        string? subjectId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogDebug(
                "Retrieving admin notifications for subject {SubjectId}",
                subjectId ?? "anonymous"
            );

            var notifications = _notifications.ListAdminNotifications(_tenantAccessor.TenantId);

            // Check if user has admin permissions (implements legacy authorization check)
            var isAdmin = false;
            if (!string.IsNullOrEmpty(subjectId))
            {
                // Check for full admin permissions equivalent to '*:*:admin' in the legacy system
                isAdmin = await _authorizationService.CheckPermissionAsync(subjectId, "*:*:admin");
                _logger.LogDebug(
                    "Admin permission check for subject {SubjectId}: {IsAdmin}",
                    subjectId,
                    isAdmin
                );
            }

            var response = new AdminNotifiesResponse
            {
                Status = 200,
                Message = new AdminNotifiesMessage
                {
                    // Only return notification details to admin users (1:1 legacy compatibility)
                    Notifies = isAdmin ? notifications : new List<AdminNotification>(),
                    NotifyCount = notifications.Count,
                },
            };

            _logger.LogDebug(
                "Returning {Count} notifications, details visible: {IsAdmin}",
                notifications.Count,
                isAdmin
            );

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving admin notifications");
            return new AdminNotifiesResponse
            {
                Status = 500,
                Message = new AdminNotifiesMessage
                {
                    Notifies = new List<AdminNotification>(),
                    NotifyCount = 0,
                },
            };
        }
    }

    public Task<NotificationAckResponse> AddAdminNotificationAsync(
        AdminNotification notification,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (notification == null)
            {
                return Task.FromResult(
                    new NotificationAckResponse
                    {
                        Success = false,
                        Message = "Notification cannot be null",
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    }
                );
            }

            // Implement legacy adminnotifies.addNotify() behavior
            notification.Title = string.IsNullOrEmpty(notification.Title)
                ? "None"
                : notification.Title;
            notification.Message = string.IsNullOrEmpty(notification.Message)
                ? "None"
                : notification.Message;

            var count = _notifications.AddAdminNotification(_tenantAccessor.TenantId, notification);
            _logger.LogDebug(
                "Recorded admin notification: {Message}, count: {Count}",
                notification.Message,
                count
            );

            return Task.FromResult(
                new NotificationAckResponse
                {
                    Success = true,
                    Message = "Admin notification added successfully",
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding admin notification");
            return Task.FromResult(
                new NotificationAckResponse
                {
                    Success = false,
                    Message = "Internal error adding admin notification",
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                }
            );
        }
    }

    public Task ClearAllAdminNotificationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Clear all notifications
            _notifications.ClearAdminNotifications(_tenantAccessor.TenantId);

            _logger.LogInformation("Cleared all admin notifications");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing all admin notifications");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Registers a Pushover receipt for an alarm to enable proper acknowledgment mapping
    /// This method should be called when a Pushover notification is sent for an alarm
    /// </summary>
    /// <param name="receipt">Pushover receipt ID</param>
    /// <param name="level">Alarm level</param>
    /// <param name="group">Alarm group</param>
    public void RegisterPushoverReceipt(string receipt, int level, string group)
    {
        if (string.IsNullOrEmpty(receipt))
        {
            _logger.LogWarning("Cannot register Pushover receipt: receipt is null or empty");
            return;
        }

        _notifications.RegisterReceipt(_tenantAccessor.TenantId, receipt, level, group);

        _logger.LogDebug(
            "Registered Pushover receipt {Receipt} for alarm level {Level}, group {Group}",
            receipt,
            level,
            group
        );
    }

    /// <summary>
    /// Gets the count of active Pushover receipts for monitoring/debugging
    /// </summary>
    /// <returns>Number of active Pushover receipts</returns>
    public int GetActivePushoverReceiptCount()
    {
        return _notifications.CountActiveReceipts(_tenantAccessor.TenantId);
    }

    /// <summary>
    /// Sends a Pushover notification for an alarm
    /// Implements legacy Pushover notification sending with full 1:1 compatibility
    /// </summary>
    /// <param name="level">Alarm level (1=WARN, 2=URGENT)</param>
    /// <param name="group">Alarm group</param>
    /// <param name="title">Notification title</param>
    /// <param name="message">Notification message</param>
    /// <param name="sound">Pushover sound (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Response indicating success or failure</returns>
    public async Task<NotificationAckResponse> SendPushoverNotificationAsync(
        int level,
        string group,
        string title,
        string message,
        string? sound = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (_pushoverService == null)
            {
                _logger.LogDebug("Pushover service not configured, skipping notification");
                return new NotificationAckResponse
                {
                    Success = true,
                    Message = "Pushover service not configured",
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                };
            }

            // Create Pushover notification request
            var pushoverRequest = _pushoverService.CreateAlarmNotification(
                level,
                group,
                title,
                message,
                sound
            );

            _logger.LogDebug(
                "Sending Pushover notification for alarm - level: {Level}, group: {Group}, title: {Title}",
                level,
                group,
                title
            );

            // Send notification
            var pushoverResponse = await _pushoverService.SendNotificationAsync(
                pushoverRequest,
                cancellationToken
            );

            if (pushoverResponse.Success)
            {
                _logger.LogInformation(
                    "Pushover notification sent successfully - level: {Level}, group: {Group}, receipt: {Receipt}",
                    level,
                    group,
                    pushoverResponse.Receipt ?? "none"
                );

                return new NotificationAckResponse
                {
                    Success = true,
                    Message = "Pushover notification sent successfully",
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                };
            }
            else
            {
                _logger.LogWarning(
                    "Failed to send Pushover notification - level: {Level}, group: {Group}, error: {Error}",
                    level,
                    group,
                    pushoverResponse.Error
                );

                return new NotificationAckResponse
                {
                    Success = false,
                    Message = $"Failed to send Pushover notification: {pushoverResponse.Error}",
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error sending Pushover notification for alarm - level: {Level}, group: {Group}",
                level,
                group
            );

            return new NotificationAckResponse
            {
                Success = false,
                Message = "Internal error sending Pushover notification",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
        }
    }
}
