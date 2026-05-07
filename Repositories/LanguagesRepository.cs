using Medpointe.Data;
using Medpointe.Models.Patients;

namespace Medpointe.Repositories;

public sealed class LanguagesRepository(DatabaseClient databaseClient)
{
    public async Task<List<LanguageModel>> GetActive(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                "id" AS Id,
                "code" AS Code,
                "name" AS Name,
                "hl7_code" AS Hl7Code
            FROM languages
            WHERE "active" = TRUE
            ORDER BY
                CASE lower("name") WHEN 'english' THEN 0 ELSE 1 END,
                "name";
            """;

        return await databaseClient.GetListByQuery<LanguageModel>(sql, cancellationToken: cancellationToken);
    }
}
