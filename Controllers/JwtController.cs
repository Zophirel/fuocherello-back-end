using Microsoft.AspNetCore.Mvc;
using Fuocherello.Data;
using Fuocherello.Singleton.JwtManager;
namespace Fuocherello.Controllers;

[ApiController]
[Route("api/[controller]")]

public class JwtController : ControllerBase
{
    private readonly ApiServerContext _context;
    private readonly IJwtManager? _manager;
    private readonly IConfiguration _configuration;
    public JwtController(ApiServerContext _context,  IConfiguration configuration, JwtManager manager)
    {
        this._context = _context;
        _manager = manager;
        _configuration = configuration;
    }

    /*
    * 200 Ok                  = access token valido
    *
    * 401 Unauthorized        = access token scaduto, 
    *                           segnala di richiamare 
    *                           con il refresh token
    *
    * 200 Ok(token)           = refresh token valido 
    *
    * 403 Forbid              = refresh token scaduto, si chiede all'utente di eseguire il login
    */
    [HttpGet]
    public ActionResult Validate([FromHeader(Name = "Authentication")] string Token)
    {
        string? type = _manager!.ExtractType(Token); 
        Console.WriteLine(type);
        if(type == "Access"){
            MyStatusCodeResult valid = _manager.ValidateAccessToken(Token); 
            return StatusCode(valid.StatusCode);
        }else if(type == "Refresh"){
            MyStatusCodeResult valid = _manager.ValidateRefreshToken(Token); 
            return StatusCode(valid.StatusCode, valid.Result);
        }
        return Forbid();
    }
}

