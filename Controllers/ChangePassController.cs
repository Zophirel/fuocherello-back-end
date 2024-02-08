using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using final.Data;
using final.Models;
using final.Services.EmailService;

namespace final.Controllers;

[ApiController]
[Route("[controller]")]
public class ChangePassController : ControllerBase
{
    private static JwtManager? _manager;
    private readonly ApiServerContext _context;
    private readonly NpgsqlDataSource _conn;
    public ChangePassController(ApiServerContext context, NpgsqlDataSource conn, RSA key){
        _conn = conn;
        _context = context;
        _manager = JwtManager.GetInstance(key);
    }

    private static Task _SendEmail(string id, string emailTo)
    {
        IConfiguration configuration = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build();                   
        var service = new EmailService(configuration);
        EmailDTO email = new();
        string token = _manager!.GenChangePassToken(id);
        string encodedToken = _manager.encode(token);
        string reoverPassLink = $"https://www.zophirel.it:8443/signup/privato/redirect?url=fuocherello://changepass/{encodedToken}";
        email.Subject = "Fuocherello - Richiesta cambio password";
        email.Body = 
        $"""
            Clicca <a href="{reoverPassLink}">qui</a> per cambiare la tua password
            <br/>
            <br/>
            <a href="{reoverPassLink}">{reoverPassLink}</a>
        """;
        email.To = emailTo;
        service.SendEmail(email);
        Console.WriteLine("email mandata");
        return Task.CompletedTask;
    }

    [HttpGet]
    public async Task<IActionResult> GetChangePassTokenAsync([FromHeader] string emailTo){  
        try{
            var user = _context.utente.SingleOrDefault(user => user.email == emailTo);
            if(user is not null){
                var id = user.hashed_id;
                await _SendEmail(id!.ToString(), emailTo);
                return Ok();
            }else{
                return BadRequest();
            }
            
        }catch(Exception e) {
            Console.WriteLine(e);
            return BadRequest();
        }
    }

    [HttpPut("{token}")]
    public ActionResult ChangePassword(string token, [FromHeader] string password){
        
        var decodedToken = _manager!.decode(token);
        MyStatusCodeResult? isValid = _manager.ValidateRecoverPassToken(decodedToken);

        if(isValid!.statusCode == 200){
            var UserId = _manager.ExtractSub(decodedToken);
            Utente? user = _context.utente.SingleOrDefault(user => user.hashed_id == UserId);
            if(user is not null){
                var hashedPass = BCrypt.Net.BCrypt.HashPassword(password);
                user.password = hashedPass;
                _context.SaveChanges();
                return Ok();
            }else{
                Console.WriteLine("utente non trovato");
                return BadRequest();
            }   
        }else{
            return StatusCode(isValid.statusCode, isValid.result);
        }
    }
}