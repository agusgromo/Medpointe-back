using Medpointe.Data;
using Medpointe.Models.Patients;

namespace Medpointe.Repositories;

public class PatientsRepository(DatabaseClient databaseClient)
{
    public async Task<List<PatientModel>> Search(string search, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                "first_name" AS FirstName,
                "middle_name" AS MiddleName,
                "last_name" AS LastName
            FROM patients
            WHERE "first_name" LIKE @search OR "last_name" LIKE @search;
            """;

        return await databaseClient.GetListByQuery<PatientModel>(sql, new { search = $"{search}%" }, cancellationToken);
    }
}
