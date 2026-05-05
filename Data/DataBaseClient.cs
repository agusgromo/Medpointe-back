using System.Data;
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
}