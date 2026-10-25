using System.Collections.Concurrent;

namespace Nocturne.API.Services.Caching;

/// <summary>
/// The loads of cold <see cref="TenantCache"/> keys in progress, so concurrent readers of one full
/// key run one load between them. A singleton: the key already names tenant, partition and demo
/// segment, so two partitions never share a run.
/// </summary>
/// <remarks>
/// A load's factory captures its own caller's request scope and token, so the caller that starts
/// a run awaits its own factory, never anyone else's. A joiner that sees the run cancelled by a
/// token that is not its own starts again; any other failure reaches every joiner.
/// </remarks>
public sealed class CacheFlights
{
    private readonly ConcurrentDictionary<(string Key, Type Type), Task> _flights = new();

    public async Task<T> RunAsync<T>(string key, Func<Task<T>> load, CancellationToken ct)
    {
        var slot = (key, typeof(T));
        while (true)
        {
            var flight = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            var running = _flights.GetOrAdd(slot, flight.Task);
            if (running != flight.Task)
            {
                try
                {
                    return await ((Task<T>)running).WaitAsync(ct);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    continue;
                }
            }

            T value;
            try
            {
                value = await load();
            }
            catch (Exception ex)
            {
                _flights.TryRemove(KeyValuePair.Create(slot, (Task)flight.Task));
                flight.SetException(ex);
                return await flight.Task;
            }

            _flights.TryRemove(KeyValuePair.Create(slot, (Task)flight.Task));
            flight.SetResult(value);
            return value;
        }
    }
}
