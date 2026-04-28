using cz7_s33286_db_api.DTOs;

namespace cz7_s33286_db_api.Services;

public interface IAppointmentsService
{
    Task<IEnumerable<AppointmentListDto>> GetAppointmentsAsync(string? status, string? patientLastName);
    Task<AppointmentDetailsDto?> GetAppointmentByIdAsync(int idAppointment);
    Task<int> CreateAppointmentAsync(CreateAppointmentRequestDto request);
    Task<bool> UpdateAppointmentAsync(int idAppointment, UpdateAppointmentRequestDto request);
    Task<bool> DeleteAppointmentAsync(int idAppointment);
}