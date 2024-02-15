
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Fuocherello.Data;
using Fuocherello.Models;
using Fuocherello.Services.EmailService;
using Fuocherello.Singleton.JwtManager;

namespace Fuocherello.Controllers;

[ApiController]
[Route("[controller]")]
public class ChangePassController : ControllerBase
{
    private readonly IJwtManager _manager;
    private readonly ApiServerContext _context;
    private readonly NpgsqlDataSource _conn;
    public ChangePassController(ApiServerContext context, NpgsqlDataSource conn, IJwtManager manager){
        _conn = conn;
        _context = context;
        _manager = manager;
    }

    private Task _SendEmail(string id, string emailTo)
    {
        IConfiguration configuration = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build();                   
        var service = new EmailService(configuration);
        EmailDTO email = new();
        string token = _manager.GenChangePassToken(id);
        string encodedToken = _manager.Encode(token);
        string reoverPassLink = $"https://www.zophirel.it:8443/signup/privato/redirect?url=Fuocherello://changepass/{encodedToken}";
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
            var user = _context.Users.SingleOrDefault(user => user.Email == emailTo);
            if(user is not null){
                var id = user.HashedId;
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
        
        var decodedToken = _manager!.Decode(token);
        MyStatusCodeResult? isValid = _manager.ValidateRecoverPassToken(decodedToken);

        if(isValid!.StatusCode == 200){
            var UserId = _manager.ExtractSub(decodedToken);
            User? user = _context.Users.SingleOrDefault(user => user.HashedId == UserId);
            if(user is not null){
                var hashedPass = BCrypt.Net.BCrypt.HashPassword(password);
                user.Password = hashedPass;
                _context.SaveChanges();
                return Ok();
            }else{
                Console.WriteLine("User non trovato");
                return BadRequest();
            }   
        }else{
            return StatusCode(isValid.StatusCode, isValid.Result);
        }
    }
}