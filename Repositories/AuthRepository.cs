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

    public async Task CreateUserAsync(string username, string passwordHash, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO users ("username", "password")
            VALUES (@Username, @Password);
            """;

        await databaseClient.ExecuteByQuery(sql, new {username, Password = passwordHash}, cancellationToken);
    }
}
