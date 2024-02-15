using System.IdentityModel.Tokens.Jwt;
using Fuocherello.Data;
using Fuocherello.Singleton.JwtManager;
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
        private readonly IJwtManager _manager; 
        private readonly ApiServerContext _context;

        public EmailController(IEmailService emailService, NpgsqlDataSource conn, ApiServerContext context, IJwtManager manager)
        {
            _context = context;
            _emailService = emailService;
            _manager = manager;
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
                string token = _manager!.Decode(encodedToken);
                Console.WriteLine(token);
                var isValid = _manager!.ValidateVerifyEmailToken(token);
                Console.WriteLine($"is valid: {isValid}");
                if (isValid.StatusCode == 200)
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
                        User? user = _context.Users.FirstOrDefault(user => user.HashedId == sub);
                        if(user != null && !user.Verified){
                            _context.Attach(user);
                            user.Verified = true;
                            _context.Entry(user).Property(p => p.Verified).IsModified = true;
                            await _context.SaveChangesAsync();
                            return Ok();
                        }
                    }
                }
                return StatusCode(isValid.StatusCode);
            }catch(Exception e){
                Console.WriteLine(e.Message);
                return BadRequest();
            }
        }
    }
}
