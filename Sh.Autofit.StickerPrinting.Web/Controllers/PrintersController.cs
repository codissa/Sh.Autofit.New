using Microsoft.AspNetCore.Mvc;
using Sh.Autofit.StickerPrinting.Services.Printing.Abstractions;

namespace Sh.Autofit.StickerPrinting.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PrintersController : ControllerBase
{
    private readonly IPrinterService _printerService;

    public PrintersController(IPrinterService printerService)
    {
        _printerService = printerService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var printers = await _printerService.GetAvailablePrintersAsync();
        return Ok(printers);
    }

    [HttpGet("{name}/status")]
    public async Task<IActionResult> GetStatus(string name)
    {
        var status = await _printerService.GetPrinterStatusAsync(name);
        return Ok(status);
    }
}
