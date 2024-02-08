using Microsoft.AspNetCore.Mvc;
using final.Data;
using final.Models;
using System.Security.Cryptography;

namespace final.Controllers;

[ApiController]
[Route("api/[controller]")]

public class JwtController : ControllerBase
{
    private readonly ApiServerContext _context;
    private static JwtManager? _manager;
    private readonly IConfiguration _configuration;
    public JwtController(ApiServerContext _context, RSA key, IConfiguration configuration)
    {
        this._context = _context;
        _manager = JwtManager.GetInstance(key);
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
            return StatusCode(valid.statusCode);
        }else if(type == "Refresh"){
            MyStatusCodeResult valid = _manager.ValidateRefreshToken(Token); 
            return StatusCode(valid.statusCode, valid.result);
        }
        return Forbid();
    }
}

