using Dapper;
using Medpointe.Data;
using Medpointe.Models.Auth;

namespace Medpointe.Repositories;

public class AuthRepository(DatabaseClient databaseClient)
{
    public async Task<LoginRequest?> GetByUsername(string username, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                "username" AS Username,
                "password" AS Password
            FROM users
            WHERE LOWER("username") = @Username;
            """;

        return await databaseClient.GetOneByQuery<LoginRequest>(sql, new {username}, cancellationToken);
    }

    public async Task<DashboardContextResponse?> GetDashboardContextAsync(string username, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                NULLIF(BTRIM(SPLIT_PART(p.name, ',', 1)), '') AS ProviderName,
                u.default_location_id AS DefaultLocationId
            FROM users u
            LEFT JOIN providers p ON p.id = u.default_provider_id
            WHERE LOWER(u.username) = @Username;
            """;

        return await databaseClient.GetOneByQuery<DashboardContextResponse>(sql, new { Username = username }, cancellationToken);
    }

    public async Task CreateUserAsync(string username, string passwordHash, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO users ("username", "password")
            VALUES (@Username, @Password);
            """;

        await databaseClient.ExecuteByQuery(sql, new {username, Password = passwordHash}, cancellationToken);
    }
}
