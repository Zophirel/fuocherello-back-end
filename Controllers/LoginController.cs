using Microsoft.AspNetCore.Mvc;
using Fuocherello.Models;
using Fuocherello.Data;
using Fuocherello.Singleton.JwtManager;
using Fuocherello.Services.EmailService;
using System.Text;


namespace Fuocherello.Controllers;

[ApiController]
[Route("[controller]")]

public class LoginController : ControllerBase
{
    private static readonly char[] padding = { '=' };
    private readonly ApiServerContext _context;
    private readonly IJwtManager _manager;
    private readonly IConfiguration _configuration;
    private static readonly Dictionary<string, DateTime> EmailLastSent = new Dictionary<string, DateTime>();
    private static readonly object LockObject = new object();
    public LoginController(ApiServerContext context, IJwtManager manager, IConfiguration configuration)
    {
        _context = context;
        _manager = manager;
        _configuration = configuration;
    }

    private static bool CanSendEmail(string email)
    {
        lock (LockObject)
        {
            if (EmailLastSent.ContainsKey(email))
            {
                DateTime lastSentTime = EmailLastSent[email];
                TimeSpan timeSinceLastSent = DateTime.Now - lastSentTime;
                Console.WriteLine($"User cannot receive email for: {timeSinceLastSent.TotalMinutes} minutes");
                if (timeSinceLastSent.TotalMinutes < 5) 
                {
                    EmailLastSent[email] = lastSentTime.AddMinutes(5);
                    return false;
                }
            }else{
                EmailLastSent.Add(email, DateTime.Now.AddMinutes(5));
            }
            return true;
        }
    }

    private bool SendEmail(UserDTO GuestUser, string id)
    {
        try
        {
            if (GuestUser.Email != null)
            {
                var service = new EmailService(_configuration);
                string verifyToken = _manager.GenEmailVerifyToken(id);
                string encodedToken = System.Convert.ToBase64String(Encoding.ASCII.GetBytes(verifyToken))
                .TrimEnd(padding).Replace('+', '-').Replace('/', '_');

                string verifyLink = $"https://www.zophirel.it:8443/redirect?url=Fuocherello://verifyemail/{encodedToken}";

                Console.WriteLine("sending email");
                EmailDTO email = new()
                {
                    Subject = "Fuocherello - Completa la registrazione!",
                    Body =
                $@"
                    Clicca <a href=""{verifyLink}"">qui</a> per completare la tua registrazione o copia nella barra di ricerca il link seguente
                    <br/>
                    <br/>
                    <a href=""{verifyLink}"">{verifyLink}</a>
                ",
                    To = GuestUser.Email
                };

                if (service.SendEmail(email))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    
    [HttpPost]
    public ActionResult PostLogin([FromBody] Login GuestUser){ 
        try{
            var user = _context.Users.FirstOrDefault(user => user.Email == GuestUser.Email);

            if(user == null){
                throw new Exception("Email non registrata");
            }
            else if(!BCrypt.Net.BCrypt.Verify(GuestUser.Password, user!.Password)){
                throw new Exception("Credenziali errate");
            }
            else if(!user.Verified){
                // Resend email if user try to login with a non verified account    
                UserDTO resendUserEmail = new(){Email = user.Email};
            
                if(CanSendEmail(user.Email!)){
                    SendEmail(resendUserEmail, user.HashedId!);
                    throw new Exception("E-mail non verificata, verra' inviata una nuova email di conferma");
                
                }else{
                    throw new Exception("E-mail di verifica gia' inviata, si prega di riprovare piu' tardi");
                }
                
            }
            else{
                string result = $"{_manager!.GenIdToken(user)}@{_manager.GenAccessToken(user.HashedId!)}@{_manager.GenRefreshToken(user.HashedId!)}";
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
