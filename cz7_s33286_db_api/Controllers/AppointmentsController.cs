using cz7_s33286_db_api.Services;
using Microsoft.AspNetCore.Mvc;

namespace cz7_s33286_db_api.Controllers;

[Route ("api/[controller]")]
[ApiController]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointsService _appointsService;

    public AppointmentsController(IAppointsService appointsService)
    {
        _appointsService = appointsService;
    }
    
    
}