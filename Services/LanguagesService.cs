using Medpointe.Models.Patients;
using Medpointe.Repositories;

namespace Medpointe.Services;

public sealed class LanguagesService(LanguagesRepository languagesRepository)
{
    public async Task<List<LanguageModel>> GetActive(CancellationToken cancellationToken)
    {
        return await languagesRepository.GetActive(cancellationToken);
    }
}
