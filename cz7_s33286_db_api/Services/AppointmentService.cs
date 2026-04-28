using cz7_s33286_db_api.DTOs;
using Microsoft.Data.SqlClient;

namespace cz7_s33286_db_api.Services;

public class AppointmentService : IAppointmentsService
{
    private readonly IConfiguration _configuration;

    public AppointmentService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private string GetConnectionString() => _configuration.GetConnectionString("DefaultConnection")!;

    public async Task<IEnumerable<AppointmentListDto>> GetAppointmentsAsync(string? status, string? patientLastName)
    {
        var result = new List<AppointmentListDto>();
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();

        var sql = """
            SELECT a.IdAppointment, a.AppointmentDate, a.Status, a.Reason, 
                   p.FirstName + ' ' + p.LastName, p.Email
            FROM dbo.Appointments a
            JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
            WHERE (@S IS NULL OR a.Status = @S) AND (@L IS NULL OR p.LastName = @L)
            ORDER BY a.AppointmentDate;
            """;

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@S", (object?)status ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@L", (object?)patientLastName ?? DBNull.Value);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new AppointmentListDto {
                IdAppointment = reader.GetInt32(0),
                AppointmentDate = reader.GetDateTime(1),
                Status = reader.GetString(2),
                Reason = reader.GetString(3),
                PatientFullName = reader.GetString(4),
                PatientEmail = reader.GetString(5)
            });
        }
        return result;
    }

    public async Task<AppointmentDetailsDto?> GetAppointmentByIdAsync(int idAppointment)
    {
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();

        var sql = """
            SELECT a.IdAppointment, a.AppointmentDate, a.Status, a.Reason, a.InternalNotes, a.CreatedAt,
                   p.FirstName + ' ' + p.LastName, p.Email, p.PhoneNumber,
                   d.FirstName + ' ' + d.LastName, d.LicenseNumber
            FROM dbo.Appointments a
            JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
            JOIN dbo.Doctors d ON d.IdDoctor = a.IdDoctor
            WHERE a.IdAppointment = @Id;
            """;

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", idAppointment);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new AppointmentDetailsDto {
                IdAppointment = reader.GetInt32(0),
                AppointmentDate = reader.GetDateTime(1),
                Status = reader.GetString(2),
                Reason = reader.GetString(3),
                InternalNotes = reader.IsDBNull(4) ? null : reader.GetString(4),
                CreatedAt = reader.GetDateTime(5),
                PatientFullName = reader.GetString(6),
                PatientEmail = reader.GetString(7),
                PatientPhoneNumber = reader.GetString(8),
                DoctorFullName = reader.GetString(9),
                DoctorLicenseNumber = reader.GetString(10)
            };
        }
        return null;
    }

    public async Task<int> CreateAppointmentAsync(CreateAppointmentRequestDto request)
    {
        if (request.AppointmentDate < DateTime.Now) throw new ArgumentException("Data w przeszłości.");

        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();

        var checkSql = "SELECT (SELECT COUNT(1) FROM dbo.Patients WHERE IdPatient = @P AND IsActive = 1), (SELECT COUNT(1) FROM dbo.Doctors WHERE IdDoctor = @D AND IsActive = 1)";
        await using var cmdCheck = new SqlCommand(checkSql, connection);
        cmdCheck.Parameters.AddWithValue("@P", request.IdPatient);
        cmdCheck.Parameters.AddWithValue("@D", request.IdDoctor);
        await using var r = await cmdCheck.ExecuteReaderAsync();
        await r.ReadAsync();
        if (r.GetInt32(0) == 0 || r.GetInt32(1) == 0) throw new ArgumentException("Nieaktywny pacjent/lekarz.");
        await r.CloseAsync();

        var conflictSql = "SELECT COUNT(1) FROM dbo.Appointments WHERE IdDoctor = @D AND AppointmentDate = @Date AND Status != 'Cancelled'";
        await using var cmdConflict = new SqlCommand(conflictSql, connection);
        cmdConflict.Parameters.AddWithValue("@D", request.IdDoctor);
        cmdConflict.Parameters.AddWithValue("@Date", request.AppointmentDate);
        if ((int)await cmdConflict.ExecuteScalarAsync() > 0) throw new InvalidOperationException("Konflikt terminu.");

        var insertSql = "INSERT INTO dbo.Appointments (IdPatient, IdDoctor, AppointmentDate, Status, Reason) OUTPUT INSERTED.IdAppointment VALUES (@P, @D, @Date, 'Scheduled', @R)";
        await using var cmdInsert = new SqlCommand(insertSql, connection);
        cmdInsert.Parameters.AddWithValue("@P", request.IdPatient);
        cmdInsert.Parameters.AddWithValue("@D", request.IdDoctor);
        cmdInsert.Parameters.AddWithValue("@Date", request.AppointmentDate);
        cmdInsert.Parameters.AddWithValue("@R", request.Reason);
        return (int)await cmdInsert.ExecuteScalarAsync();
    }

    public async Task<bool> UpdateAppointmentAsync(int idAppointment, UpdateAppointmentRequestDto request)
    {
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();

        var getSql = "SELECT Status, AppointmentDate FROM dbo.Appointments WHERE IdAppointment = @Id";
        await using var cmdGet = new SqlCommand(getSql, connection);
        cmdGet.Parameters.AddWithValue("@Id", idAppointment);
        await using var rGet = await cmdGet.ExecuteReaderAsync();
        if (!await rGet.ReadAsync()) return false;
        var curS = rGet.GetString(0);
        var curD = rGet.GetDateTime(1);
        await rGet.CloseAsync();

        if (curS == "Completed" && curD != request.AppointmentDate) throw new ArgumentException("Nie można zmienić daty zakończonej wizyty.");

        var updateSql = "UPDATE dbo.Appointments SET IdPatient=@P, IdDoctor=@D, AppointmentDate=@Date, Status=@S, Reason=@R, InternalNotes=@N WHERE IdAppointment=@Id";
        await using var cmdUpd = new SqlCommand(updateSql, connection);
        cmdUpd.Parameters.AddWithValue("@P", request.IdPatient);
        cmdUpd.Parameters.AddWithValue("@D", request.IdDoctor);
        cmdUpd.Parameters.AddWithValue("@Date", request.AppointmentDate);
        cmdUpd.Parameters.AddWithValue("@S", request.Status);
        cmdUpd.Parameters.AddWithValue("@R", request.Reason);
        cmdUpd.Parameters.AddWithValue("@N", (object?)request.InternalNotes ?? DBNull.Value);
        cmdUpd.Parameters.AddWithValue("@Id", idAppointment);
        await cmdUpd.ExecuteNonQueryAsync();
        return true;
    }

    public async Task<bool> DeleteAppointmentAsync(int idAppointment)
    {
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();

        var statusSql = "SELECT Status FROM dbo.Appointments WHERE IdAppointment = @Id";
        await using var cmdStatus = new SqlCommand(statusSql, connection);
        cmdStatus.Parameters.AddWithValue("@Id", idAppointment);
        var s = await cmdStatus.ExecuteScalarAsync();
        if (s == null) return false;
        if (s.ToString() == "Completed") throw new InvalidOperationException("Nie można usunąć zakończonej.");

        await using var cmdDel = new SqlCommand("DELETE FROM dbo.Appointments WHERE IdAppointment = @Id", connection);
        cmdDel.Parameters.AddWithValue("@Id", idAppointment);
        await cmdDel.ExecuteNonQueryAsync();
        return true;
    }
}