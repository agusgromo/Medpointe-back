using Medpointe.Models.Clinical;
using Medpointe.Models.Patients;
using Medpointe.Models.Schedule;
using Medpointe.Repositories;

namespace Medpointe.Services;

public sealed class ClinicalService(
    PatientsRepository patientsRepository,
    ScheduleRepository scheduleRepository)
{
    public async Task<ClinicalChartModel?> GetChart(
        long patientId,
        long? appointmentId,
        string? username,
        CancellationToken cancellationToken)
    {
        if (patientId <= 0)
        {
            return null;
        }

        PatientActivityHeader? patient = await patientsRepository.GetActivityHeader(patientId, cancellationToken);

        if (patient is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(username))
        {
            await patientsRepository.RememberPatientView(username.Trim().ToLowerInvariant(), patientId, cancellationToken);
        }

        PatientContactSummary? contact = await patientsRepository.GetContact(patientId, cancellationToken);
        List<PatientPharmacySummary> pharmacies = await patientsRepository.GetPharmacies(patientId, cancellationToken);
        List<InsurancePolicySummary> insurancePolicies = await patientsRepository.GetInsurancePolicies(patientId, cancellationToken);
        List<VisitSummary> encounters = await patientsRepository.GetVisits(patientId, cancellationToken);
        List<PatientProblemSummary> problems = await patientsRepository.GetProblems(patientId, cancellationToken);
        List<PatientAllergySummary> allergies = await patientsRepository.GetAllergies(patientId, cancellationToken);
        List<PatientMedicationSummary> medications = await patientsRepository.GetMedications(patientId, cancellationToken);
        List<ClinicalOrderSummary> orders = await patientsRepository.GetOrders(patientId, cancellationToken);
        List<PatientNoteSummary> patientNotes = await patientsRepository.GetNotes(patientId, cancellationToken);

        long? normalizedAppointmentId = appointmentId is > 0 ? appointmentId : null;
        ScheduleAppointmentModel? appointment = null;

        if (normalizedAppointmentId is not null)
        {
            appointment = await scheduleRepository.GetAppointment(normalizedAppointmentId.Value, cancellationToken);

            if (appointment?.PatientId != patientId)
            {
                appointment = null;
            }
        }

        VisitSummary? currentVisit = normalizedAppointmentId is not null
            ? await patientsRepository.GetVisitByAppointment(patientId, normalizedAppointmentId.Value, cancellationToken)
            : null;

        currentVisit ??= encounters.FirstOrDefault();

        List<VisitDiagnosisSummary> encounterDiagnoses = currentVisit is null
            ? []
            : await patientsRepository.GetVisitDiagnoses(currentVisit.Id, cancellationToken);

        List<ClinicalNoteEntry> clinicalNotes = await patientsRepository.GetClinicalNotes(patientId, currentVisit?.Id, cancellationToken);
        List<EncounterFormSummary> encounterForms = await patientsRepository.GetEncounterForms(patientId, currentVisit?.Id, cancellationToken);

        return new ClinicalChartModel
        {
            Patient = patient,
            Contact = contact,
            Pharmacies = pharmacies,
            InsurancePolicies = insurancePolicies,
            Appointment = appointment,
            CurrentVisit = currentVisit,
            Encounters = encounters,
            EncounterDiagnoses = encounterDiagnoses,
            Problems = problems,
            Allergies = allergies,
            Medications = medications,
            Orders = orders,
            ClinicalNotes = clinicalNotes,
            PatientNotes = patientNotes,
            EncounterForms = encounterForms
        };
    }
}
