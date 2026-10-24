using Nocturne.Core.Models;

namespace Nocturne.API.Services.Notifications;

/// <summary>
/// The Pushover receipts and admin notifications of the legacy v1 notification API, per tenant.
/// One per instance, because <see cref="NotificationV1Service"/> lives for one request and both must
/// outlast it. A receipt lasts <see cref="ReceiptLifetime"/> and a non-persistent admin notification
/// <see cref="AdminNotificationLifetime"/>; expired entries go on access. A tenant holds at most
/// <see cref="MaxReceiptsPerTenant"/> receipts and <see cref="MaxAdminNotificationsPerTenant"/> admin
/// notifications, and gives up the oldest at the cap. A receipt group is cut to
/// <see cref="LegacyAlarmSnoozeStore.MaxGroupLength"/>, and an admin notification title and message to
/// <see cref="MaxTextLength"/>, so a caller cannot grow the store without bound.
/// </summary>
public sealed class LegacyNotificationStore(TimeProvider timeProvider)
{
    internal const int MaxReceiptsPerTenant = 5000;
    internal const int MaxAdminNotificationsPerTenant = 500;
    internal const int MaxTextLength = 1024;
    internal static readonly TimeSpan ReceiptLifetime = TimeSpan.FromHours(24);
    internal static readonly TimeSpan AdminNotificationLifetime = TimeSpan.FromHours(9);

    public readonly record struct PushoverReceipt(int Level, string Group);

    private readonly Dictionary<(Guid TenantId, string Receipt), (PushoverReceipt Alarm, DateTimeOffset CreatedAt)> _receipts = [];
    private readonly Dictionary<(Guid TenantId, string Message), AdminNotification> _adminNotifications = [];
    private readonly Lock _gate = new();

    public void RegisterReceipt(Guid tenantId, string receipt, int level, string group)
    {
        var now = timeProvider.GetUtcNow();
        lock (_gate)
        {
            RemoveExpiredReceipts(tenantId, now);
            var key = (tenantId, receipt);
            if (!_receipts.ContainsKey(key) && CountReceipts(tenantId) >= MaxReceiptsPerTenant)
                _receipts.Remove(_receipts.Where(e => e.Key.TenantId == tenantId).MinBy(e => e.Value.CreatedAt).Key);

            _receipts.TryAdd(key, (new PushoverReceipt(level, Cut(group, LegacyAlarmSnoozeStore.MaxGroupLength)), now));
        }
    }

    public bool TryGetReceipt(Guid tenantId, string receipt, out PushoverReceipt alarm)
    {
        var now = timeProvider.GetUtcNow();
        lock (_gate)
        {
            RemoveExpiredReceipts(tenantId, now);
            var found = _receipts.TryGetValue((tenantId, receipt), out var entry);
            alarm = entry.Alarm;
            return found;
        }
    }

    public void RemoveReceipt(Guid tenantId, string receipt)
    {
        lock (_gate)
            _receipts.Remove((tenantId, receipt));
    }

    public int CountActiveReceipts(Guid tenantId)
    {
        var now = timeProvider.GetUtcNow();
        lock (_gate)
        {
            RemoveExpiredReceipts(tenantId, now);
            return CountReceipts(tenantId);
        }
    }

    /// <summary>
    /// Records the notification, or counts one more occurrence of the notification that has the same
    /// message. Returns the occurrence count.
    /// </summary>
    public int AddAdminNotification(Guid tenantId, AdminNotification notification)
    {
        var nowMs = timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        var title = Cut(notification.Title, MaxTextLength);
        var message = Cut(notification.Message, MaxTextLength);
        lock (_gate)
        {
            RemoveExpiredAdminNotifications(tenantId, nowMs);
            var key = (tenantId, message);
            if (_adminNotifications.TryGetValue(key, out var existing))
            {
                existing.Count += 1;
                existing.LastRecorded = nowMs;
                return existing.Count;
            }

            if (_adminNotifications.Count(e => e.Key.TenantId == tenantId) >= MaxAdminNotificationsPerTenant)
                _adminNotifications.Remove(_adminNotifications.Where(e => e.Key.TenantId == tenantId).MinBy(e => e.Value.LastRecorded).Key);

            _adminNotifications[key] = new AdminNotification
            {
                Title = title,
                Message = message,
                Count = 1,
                LastRecorded = notification.LastRecorded == 0 ? nowMs : notification.LastRecorded,
                Persistent = notification.Persistent,
            };
            return 1;
        }
    }

    public List<AdminNotification> ListAdminNotifications(Guid tenantId)
    {
        var nowMs = timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        lock (_gate)
        {
            RemoveExpiredAdminNotifications(tenantId, nowMs);
            return _adminNotifications
                .Where(e => e.Key.TenantId == tenantId)
                .Select(e => new AdminNotification
                {
                    Title = e.Value.Title,
                    Message = e.Value.Message,
                    Count = e.Value.Count,
                    LastRecorded = e.Value.LastRecorded,
                    Persistent = e.Value.Persistent,
                })
                .ToList();
        }
    }

    public void ClearAdminNotifications(Guid tenantId)
    {
        lock (_gate)
        {
            foreach (var key in _adminNotifications.Keys.Where(k => k.TenantId == tenantId).ToList())
                _adminNotifications.Remove(key);
        }
    }

    private int CountReceipts(Guid tenantId) => _receipts.Count(e => e.Key.TenantId == tenantId);

    private static string Cut(string value, int length) =>
        value.Length > length ? value[..length] : value;

    private void RemoveExpiredReceipts(Guid tenantId, DateTimeOffset now)
    {
        var expired = _receipts
            .Where(e => e.Key.TenantId == tenantId && now - e.Value.CreatedAt > ReceiptLifetime)
            .Select(e => e.Key)
            .ToList();
        foreach (var key in expired)
            _receipts.Remove(key);
    }

    private void RemoveExpiredAdminNotifications(Guid tenantId, long nowMs)
    {
        var expired = _adminNotifications
            .Where(e => e.Key.TenantId == tenantId && !e.Value.Persistent && nowMs - e.Value.LastRecorded >= AdminNotificationLifetime.TotalMilliseconds)
            .Select(e => e.Key)
            .ToList();
        foreach (var key in expired)
            _adminNotifications.Remove(key);
    }
}
