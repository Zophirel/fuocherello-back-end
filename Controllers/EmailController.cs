using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Fuocherello.Data;
using Fuocherello.Models;
using Fuocherello.Services.EmailService;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Npgsql;

namespace Fuocherello.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmailController : ControllerBase
    {
        private readonly IEmailService _emailService;
        private readonly RSA _rsa;
        private static JwtManager? _manager; 
        private readonly ApiServerContext _context;

        public EmailController(IEmailService emailService, RSA rsa, NpgsqlDataSource conn, ApiServerContext context)
        {
            _context = context;
            _emailService = emailService;
            _rsa = rsa;
            _manager = JwtManager.GetInstance(rsaKey: _rsa);
        }

        [HttpPost]
        public IActionResult SendEmail(EmailDTO request)
        { 
            _emailService.SendEmail(request);
            return Ok();
        }

        [HttpGet("verify/{encodedToken}")]
        public async Task<ActionResult> VerifyEmail(string encodedToken) {
            try{
                string token = _manager!.decode(encodedToken);
                Console.WriteLine(token);
                var isValid = _manager!.ValidateVerifyEmailToken(token);
                Console.WriteLine($"is valid: {isValid}");
                if (isValid.statusCode == 200)
                {
                    var jwtHandler = new JwtSecurityTokenHandler();
                    var jwt = jwtHandler.ReadJwtToken(token);
                    var payloadString = jwt.Payload.SerializeToJson();
                    JObject payload = JObject.Parse(payloadString);
                    string type = payload["type"]!.ToString();
                    string sub = payload["sub"]!.ToString();
                    if (type != "VerifyEmail")
                    {
                        return Forbid();
                    }else{
                        Utente? user = _context.utente.FirstOrDefault(user => user.hashed_id == sub);
                        if(user != null && !user.verified){
                            _context.Attach(user);
                            user.verified = true;
                            _context.Entry(user).Property(p => p.verified).IsModified = true;
                            await _context.SaveChangesAsync();
                            return Ok();
                        }
                    }
                }
                return StatusCode(isValid.statusCode);
            }catch(Exception e){
                Console.WriteLine(e.Message);
                return BadRequest();
            }
        }
    }
}
