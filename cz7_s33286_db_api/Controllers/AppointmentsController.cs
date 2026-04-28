using cz7_s33286_db_api.DTOs;
using cz7_s33286_db_api.Services;
using Microsoft.AspNetCore.Mvc;

namespace cz7_s33286_db_api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentsService _appointsService;

    public AppointmentsController(IAppointmentsService appointsService)
    {
        _appointsService = appointsService;
    }

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] string? status, [FromQuery] string? patientLastName)
        => Ok(await _appointsService.GetAppointmentsAsync(status, patientLastName));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var a = await _appointsService.GetAppointmentByIdAsync(id);
        return a == null ? NotFound(new ErrorResponseDto { Message = "Brak wizyty." }) : Ok(a);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAppointmentRequestDto req)
    {
        try {
            var id = await _appointsService.CreateAppointmentAsync(req);
            return Created($"api/appointments/{id}", new { IdAppointment = id });
        }
        catch (ArgumentException ex) { return BadRequest(new ErrorResponseDto { Message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new ErrorResponseDto { Message = ex.Message }); }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateAppointmentRequestDto req)
    {
        try {
            if (!await _appointsService.UpdateAppointmentAsync(id, req)) return NotFound();
            return Ok();
        }
        catch (ArgumentException ex) { return BadRequest(new ErrorResponseDto { Message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new ErrorResponseDto { Message = ex.Message }); }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try {
            if (!await _appointsService.DeleteAppointmentAsync(id)) return NotFound();
            return NoContent();
        }
        catch (InvalidOperationException ex) { return Conflict(new ErrorResponseDto { Message = ex.Message }); }
    }
}