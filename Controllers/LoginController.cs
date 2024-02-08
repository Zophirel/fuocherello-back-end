using Microsoft.AspNetCore.Mvc;
using final.Models;
using final.Data;
using System.Security.Cryptography;

namespace final.Controllers;

[ApiController]
[Route("[controller]")]

public class LoginController : ControllerBase
{
    private readonly ApiServerContext _context;
    private static JwtManager? _manager;
    public LoginController(ApiServerContext context,  RSA key, IConfiguration configuration)
    {
        _context = context;
        _manager = JwtManager.GetInstance(key);
    }
    
    [HttpPost]
    public ActionResult PostLogin([FromBody] Login GuestUser){ 
        try{
            Console.WriteLine("ok 1");
            var user = _context.utente.FirstOrDefault(user => user.email == GuestUser.Email);
            
            if(user == null){
                throw new Exception("Email non registrata");
            }
            else if(!BCrypt.Net.BCrypt.Verify(GuestUser.Password, user!.password)){
                throw new Exception("Credenziali errate");
            }
            else if(!user.verified){
                throw new Exception("Email non verificata");   
            }
            else{
                string result = $"{_manager!.GenIdToken(user)}@{_manager.GenAccessToken(user.hashed_id!)}@{_manager.GenRefreshToken(user.hashed_id!)}";
                return Ok(result);     
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return BadRequest(e.Message);
        }
    }
}
