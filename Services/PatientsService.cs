using Medpointe.Models.Patients;
using Medpointe.Repositories;

namespace Medpointe.Services;

public sealed class PatientsService(PatientsRepository patientRepository)
{
    public async Task<List<PatientModel>> Search(string search, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return [];
        }

        List<PatientModel> patients = await patientRepository.Search(search, cancellationToken);
        return patients;
    }

    public async Task<PatientActivityModel?> GetActivity(long patientId, CancellationToken cancellationToken)
    {
        PatientActivityHeader? patient = await patientRepository.GetActivityHeader(patientId, cancellationToken);

        if (patient is null)
        {
            return null;
        }

        PatientContactSummary? contact = await patientRepository.GetContact(patientId, cancellationToken);
        List<PatientPharmacySummary> pharmacies = await patientRepository.GetPharmacies(patientId, cancellationToken);
        List<InsurancePolicySummary> insurancePolicies = await patientRepository.GetInsurancePolicies(patientId, cancellationToken);
        List<AppointmentSummary> appointments = await patientRepository.GetAppointments(patientId, cancellationToken);
        List<VisitSummary> visits = await patientRepository.GetVisits(patientId, cancellationToken);
        List<PatientProblemSummary> problems = await patientRepository.GetProblems(patientId, cancellationToken);
        List<PatientAllergySummary> allergies = await patientRepository.GetAllergies(patientId, cancellationToken);
        List<PatientMedicationSummary> medications = await patientRepository.GetMedications(patientId, cancellationToken);
        List<ClinicalOrderSummary> orders = await patientRepository.GetOrders(patientId, cancellationToken);
        List<PatientNoteSummary> notes = await patientRepository.GetNotes(patientId, cancellationToken);

        return new PatientActivityModel
        {
            Patient = patient,
            Contact = contact,
            Pharmacies = pharmacies,
            InsurancePolicies = insurancePolicies,
            Appointments = appointments,
            Visits = visits,
            Problems = problems,
            Allergies = allergies,
            Medications = medications,
            Orders = orders,
            Notes = notes,
            Timeline = BuildTimeline(appointments, visits, medications, orders, notes)
        };
    }

    public async Task<CreatePatientResult> Create(CreatePatientRequest request, CancellationToken cancellationToken)
    {
        CreatePatientRequest normalizedRequest = NormalizeCreateRequest(request);
        string? validationError = ValidateCreateRequest(normalizedRequest);

        if (validationError is not null)
        {
            return new CreatePatientResult { ErrorMessage = validationError };
        }

        List<PatientModel> duplicates = await patientRepository.FindPotentialDuplicates(normalizedRequest, cancellationToken);

        if (duplicates.Count > 0)
        {
            return new CreatePatientResult
            {
                ErrorMessage = "This patient's record may already be on file.",
                Duplicates = duplicates
            };
        }

        PatientModel patient = await patientRepository.Create(normalizedRequest, cancellationToken);

        return new CreatePatientResult { Patient = patient };
    }

    private static string? ValidateCreateRequest(CreatePatientRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) && string.IsNullOrWhiteSpace(request.LastName))
        {
            return "First Name and Last Name are required.";
        }

        if (string.IsNullOrWhiteSpace(request.FirstName))
        {
            return "First Name field is required.";
        }

        if (string.IsNullOrWhiteSpace(request.LastName))
        {
            return "Last Name field is required.";
        }

        if (request.DateOfBirth == default)
        {
            return "Date of Birth is required.";
        }

        if (request.DateOfBirth.Date > DateTime.UtcNow.Date)
        {
            return "Date of Birth cannot be in the future.";
        }

        if (string.IsNullOrWhiteSpace(request.SexAtBirth))
        {
            return "Sex is a required field.";
        }

        if (!new[] { "male", "female", "unknown" }.Contains(request.SexAtBirth))
        {
            return "Sex must be male, female, or unknown.";
        }

        return null;
    }

    private static CreatePatientRequest NormalizeCreateRequest(CreatePatientRequest request)
    {
        return new CreatePatientRequest
        {
            FirstName = ReCap(RequiredTrim(request.FirstName)),
            MiddleName = NormalizeInitial(request.MiddleName),
            LastName = ReCap(RequiredTrim(request.LastName)),
            Suffix = BlankToNull(request.Suffix),
            Nickname = BlankToNull(request.Nickname),
            DateOfBirth = request.DateOfBirth.Date,
            SexAtBirth = NormalizeSex(request.SexAtBirth),
            GenderIdentity = BlankToNull(request.GenderIdentity),
            Pronouns = BlankToNull(request.Pronouns),
            MaritalStatus = BlankToNull(request.MaritalStatus),
            EmploymentStatus = BlankToNull(request.EmploymentStatus),
            PreferredLanguageId = request.PreferredLanguageId,
            Ethnicity = BlankToNull(request.Ethnicity),
            Classification = BlankToNull(request.Classification),
            Category = BlankToNull(request.Category),
            Stage = BlankToNull(request.Stage),
            AddressLine1 = ReCapOptional(BlankToNull(request.AddressLine1)),
            AddressLine2 = ReCapOptional(BlankToNull(request.AddressLine2)),
            City = ReCapOptional(BlankToNull(request.City)),
            State = BlankToNull(request.State)?.ToUpperInvariant(),
            PostalCode = BlankToNull(request.PostalCode),
            HomePhone = BlankToNull(request.HomePhone),
            WorkPhone = BlankToNull(request.WorkPhone),
            MobilePhone = BlankToNull(request.MobilePhone),
            Email = BlankToNull(request.Email)?.ToLowerInvariant(),
            CommunicationPreference = BlankToNull(request.CommunicationPreference)
        };
    }

    private static string RequiredTrim(string? value) => value?.Trim() ?? string.Empty;

    private static string? BlankToNull(string? value)
    {
        string? trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string? NormalizeInitial(string? value)
    {
        string? trimmed = BlankToNull(value);
        return trimmed is null ? null : trimmed[..1].ToUpperInvariant();
    }

    private static string ReCap(string value) =>
        string.IsNullOrWhiteSpace(value) || value.Length == 1
            ? value.ToUpperInvariant()
            : value[..1].ToUpperInvariant() + value[1..];

    private static string? ReCapOptional(string? value) => value is null ? null : ReCap(value);

    private static string NormalizeSex(string? sex)
    {
        string normalized = sex?.Trim().ToLowerInvariant() ?? string.Empty;

        return normalized switch
        {
            "m" => "male",
            "f" => "female",
            "u" => "unknown",
            "" => "",
            _ => normalized
        };
    }

    private static List<PatientTimelineItem> BuildTimeline(
        IEnumerable<AppointmentSummary> appointments,
        IEnumerable<VisitSummary> visits,
        IEnumerable<PatientMedicationSummary> medications,
        IEnumerable<ClinicalOrderSummary> orders,
        IEnumerable<PatientNoteSummary> notes)
    {
        List<PatientTimelineItem> timeline = [];

        timeline.AddRange(appointments.Select(appointment => new PatientTimelineItem
        {
            Type = "appointment",
            Title = appointment.AppointmentTypeName ?? "Appointment",
            Detail = JoinDetails(appointment.ProviderName, appointment.LocationName, appointment.Reason),
            OccurredAt = appointment.ScheduledStart,
            Status = appointment.Status
        }));

        timeline.AddRange(visits.Select(visit => new PatientTimelineItem
        {
            Type = "visit",
            Title = visit.VisitType ?? "Visit",
            Detail = JoinDetails(visit.ProviderName, visit.ChiefComplaint, FormatVitals(visit)),
            OccurredAt = visit.VisitDate,
            Status = visit.Status
        }));

        timeline.AddRange(medications
            .Where(medication => medication.StartDate is not null)
            .Select(medication => new PatientTimelineItem
            {
                Type = "medication",
                Title = medication.MedicationName,
                Detail = JoinDetails(medication.Strength, medication.Dose, medication.Frequency),
                OccurredAt = medication.StartDate!.Value,
                Status = medication.Status
            }));

        timeline.AddRange(orders.Select(order => new PatientTimelineItem
        {
            Type = order.OrderType,
            Title = order.Description,
            Detail = JoinDetails(order.Code, order.OrderedByProviderName, order.Priority),
            OccurredAt = order.OrderedAt,
            Status = order.Status
        }));

        timeline.AddRange(notes.Select(note => new PatientTimelineItem
        {
            Type = "note",
            Title = note.NoteType,
            Detail = note.Body.Length > 140 ? $"{note.Body[..140]}..." : note.Body,
            OccurredAt = note.CreatedAt,
            Status = null
        }));

        return [.. timeline
            .OrderByDescending(item => item.OccurredAt)
            .ThenBy(item => item.Type)
            .Take(40)];
    }

    private static string? FormatVitals(VisitSummary visit)
    {
        List<string> values = [];

        if (visit.SystolicBp is not null && visit.DiastolicBp is not null)
        {
            values.Add($"BP {visit.SystolicBp:0}/{visit.DiastolicBp:0}");
        }

        if (visit.HeartRate is not null)
        {
            values.Add($"HR {visit.HeartRate:0}");
        }

        if (visit.Bmi is not null)
        {
            values.Add($"BMI {visit.Bmi:0.0}");
        }

        return values.Count == 0 ? null : string.Join(" | ", values);
    }

    private static string? JoinDetails(params string?[] values)
    {
        string[] parts = [.. values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())];

        return parts.Length == 0 ? null : string.Join(" | ", parts);
    }
}
