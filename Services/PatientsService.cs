using Medpointe.Models.Patients;
using Medpointe.Repositories;

namespace Medpointe.Services;

public sealed class PatientsService(PatientsRepository patientRepository)
{
    public async Task<List<PatientModel>> Search(string search, CancellationToken cancellationToken)
    {
        List<PatientModel> patients = await patientRepository.Search(search, cancellationToken);
        return patients;
    }
}