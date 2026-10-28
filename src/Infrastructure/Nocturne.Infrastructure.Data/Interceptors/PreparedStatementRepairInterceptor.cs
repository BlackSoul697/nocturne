using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace Nocturne.Infrastructure.Data.Interceptors;

/// <summary>
/// Drops a backend's auto-prepared statements after a command on it fails in PostgreSQL, before the
/// next command runs on that backend.
/// </summary>
/// <remarks>
/// Npgsql marks a statement it is auto-preparing as prepared once PostgreSQL has parsed it, but
/// records its row description only from the Describe that follows Bind. An error raised at Bind (a
/// value too long for its column or out of range, folded into the plan; a cancellation while
/// planning) arrives between the two, so the statement stays prepared with no description. The next
/// run of it on that physical connection, whichever lessee the pool hands it to, reads its first
/// DataRow as a protocol violation ("Received unexpected backend message DataRow") and breaks the
/// connection (npgsql/npgsql#6391, unfixed in Npgsql 10.0.3). Which errors arrive at Bind depends on
/// the plan, so every server error marks the backend.
/// <para>
/// The mark is the backend's process id rather than the lease, because the repair cannot always run
/// in the lease that failed: <c>DEALLOCATE</c> is refused in an aborted transaction (25P02), and the
/// connection may go back to the pool before anything else runs on it. In an aborted transaction the
/// mark stays for the backend's next command; the command it precedes fails on the aborted
/// transaction either way. Commands sent through raw ADO.NET on a context's connection are not
/// intercepted, so their failures mark nothing.
/// </para>
/// </remarks>
public sealed class PreparedStatementRepairInterceptor : DbCommandInterceptor
{
    private readonly ConcurrentDictionary<int, byte> _unsettledBackends = new();

    /// <inheritdoc />
    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData) =>
        Mark(command, eventData.Exception);

    /// <inheritdoc />
    public override Task CommandFailedAsync(
        DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Mark(command, eventData.Exception);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Repair(command);
        return result;
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Repair(command);
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Repair(command);
        return result;
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Repair(command);
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        Repair(command);
        return result;
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Repair(command);
        return ValueTask.FromResult(result);
    }

    private void Mark(DbCommand command, Exception exception)
    {
        if (RaisedByServer(exception) && command.Connection is NpgsqlConnection { State: ConnectionState.Open } connection)
            _unsettledBackends.TryAdd(connection.ProcessID, 0);
    }

    /// <remarks>
    /// <see cref="NpgsqlConnection.UnprepareAll"/> has no asynchronous form; it is one round trip,
    /// paid only after a failure.
    /// </remarks>
    private void Repair(DbCommand command)
    {
        if (command.Connection is not NpgsqlConnection { State: ConnectionState.Open } connection
            || !_unsettledBackends.ContainsKey(connection.ProcessID))
            return;

        try
        {
            connection.UnprepareAll();
            _unsettledBackends.TryRemove(connection.ProcessID, out _);
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.InFailedSqlTransaction)
        {
        }
    }

    /// <summary>
    /// A cancellation or timeout PostgreSQL acted on surfaces with its <see cref="PostgresException"/>
    /// as an inner exception.
    /// </summary>
    private static bool RaisedByServer(Exception? exception)
    {
        for (; exception is not null; exception = exception.InnerException)
        {
            if (exception is PostgresException)
                return true;
        }

        return false;
    }
}
