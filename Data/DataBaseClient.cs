using System.Data;
using System.Data.Common;
using Dapper;

namespace Medpointe.Data;

public class DatabaseClient(IDbConnection dbConnection)
{
    public async Task<T?> GetOneByQuery<T>(string query, object? parameters = null, CancellationToken cancellationToken = default)
    {
        CommandDefinition command = new(
            query,
            parameters,
            cancellationToken: cancellationToken
        );

        return await dbConnection.QueryFirstOrDefaultAsync<T>(command);
    }
    public async Task<List<T>> GetListByQuery<T>(string query, object? parameters = null, CancellationToken cancellationToken = default)
    {
        CommandDefinition command = new(
            query,
            parameters,
            cancellationToken: cancellationToken);

        IEnumerable<T> result = await dbConnection.QueryAsync<T>(command);

        return [.. result];
    }

    public async Task<int> ExecuteByQuery(string query, object? parameters = null, CancellationToken cancellationToken = default)
    {
        CommandDefinition command = new(
            query,
            parameters,
            cancellationToken: cancellationToken);

        return await dbConnection.ExecuteAsync(command);
    }

    public async Task<T> ExecuteInTransaction<T>(
        Func<IDbConnection, IDbTransaction, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        if (dbConnection.State != ConnectionState.Open)
        {
            if (dbConnection is DbConnection db)
            {
                await db.OpenAsync(cancellationToken);
            }
            else
            {
                dbConnection.Open();
            }
        }

        using IDbTransaction transaction = dbConnection.BeginTransaction();

        try
        {
            T result = await action(dbConnection, transaction);
            transaction.Commit();
            return result;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}