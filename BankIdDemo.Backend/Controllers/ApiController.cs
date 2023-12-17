using BankIdDemo.Backend.Gateways;
using Microsoft.AspNetCore.Mvc;

namespace BankIdDemo.Backend.Controllers;

[Route("api")]
public class ApiController(IBankIdGateway bankIdGateway) : ControllerBase
{
    private string GetEndUserIp()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? throw new InvalidOperationException();
    }
    
    [HttpPost("auth")]
    public async Task<IActionResult> Auth()
    {
        return Ok(await bankIdGateway.Auth(GetEndUserIp()));
    }

    [HttpPost("sign")]
    public async Task<IActionResult> Sign()
    {
        return Ok(await bankIdGateway.Sign(GetEndUserIp()));
    }
}