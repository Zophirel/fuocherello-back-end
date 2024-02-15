using Microsoft.AspNetCore.Mvc;
using Fuocherello.Models;
using Fuocherello.Data;
using Npgsql;
using Fuocherello.Services.EmailService;
using System.Text;
using Google.Apis.Auth;
using System.Security.Cryptography;
using Fuocherello.Singleton.JwtManager;

namespace Fuocherello.Controllers;

[ApiController]
[Route("api/[controller]")]

public class SignUpController : ControllerBase
{
    private static readonly char[] padding = { '=' };
    private readonly ApiServerContext _context;
    private readonly IConfiguration _configuration;
    private readonly IJwtManager  _manager;
    public SignUpController(ApiServerContext context, IConfiguration configuration, IJwtManager manager)
    {
        _context = context;
        _configuration = configuration;
        _manager = manager;
    }

    //GET per eseguire il lancio in app tramite deeplink 
    [HttpGet("redirect")]
    public IActionResult TriggerDeepLink([FromQuery] string url){
        string[] splittedUrl = url.Substring(13).Split('/');
        string encodedToken = splittedUrl.Last();
        string pathParameter = splittedUrl[1]; 
        
        //redirect api force the server to serve only link inside the app
        url = $"Fuocherello://app/{pathParameter}/{encodedToken}";
        return new RedirectResult(url);
    }
    
    private string HmacHash(string id)
    {
        string? secretKey = _configuration.GetValue<string>("SecretKey");
        string hash = "";
        if(secretKey != null){
            byte[] secretKeyBytes = Encoding.UTF8.GetBytes(secretKey);
            byte[] messageBytes = Encoding.UTF8.GetBytes(id.ToString());

            using HMACSHA256 hmac = new(secretKeyBytes);
            byte[] hashBytes = hmac.ComputeHash(messageBytes);
            hash = Convert.ToBase64String(hashBytes);
            string url_safe_id = _manager.Encode(hash);

            Console.WriteLine("HMAC hash: " + hash);
            return url_safe_id;
        }
        return "";
    }

    private bool SendEmail(UserDTO GuestUser, string id){
    try{
        if(GuestUser.Email != null){
            var service = new EmailService(_configuration);
            string verifyToken = _manager.GenEmailVerifyToken(id);
            Console.WriteLine(verifyToken);
            Console.WriteLine($"VERIFY EMAIL TOKEN IS VALID ON GEN {_manager.ValidateVerifyEmailToken(verifyToken).StatusCode}");
            string encodedToken = System.Convert.ToBase64String(Encoding.ASCII.GetBytes(verifyToken))
            .TrimEnd(padding).Replace('+', '-').Replace('/', '_');
            
            string verifyLink = $"https://www.zophirel.it:8443/signup/privato/redirect?url=Fuocherello://verifyemail/{encodedToken}";

            Console.WriteLine("sending email");
                EmailDTO email = new()
                {
                    Subject = "Fuocherello - Completa la registrazione!",
                    Body =
                $"""
                Clicca <a href="{verifyLink}">qui</a> per completare la tua registrazione o copia nella barra di ricerca il link seguente
                <br/>
                <br/>
                <a href="{verifyLink}">{verifyLink}</a>
            """,
                    To = GuestUser.Email
                };
                if (service.SendEmail(email)){
                return true;
            }else{
                return false;
            }
        }else{
            return false;
        }
        }catch (Exception)
        {  
            return false;
        }
    }

    [HttpPost("oauthlogin")]
    public ActionResult OuathLogin([FromForm] string idToken){
        
        GoogleJsonWebSignature.Payload payload = GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = new List<string> { "746085633002-r4lsvaljlfdri7uvnio7l6l0g1ch51q2.apps.googleusercontent.com" }
        }).Result;

        Console.WriteLine($"PAYLOAD NULL {payload is not null}");
        if(payload != null){
            User? user = _context.User.SingleOrDefault(user => user.HashedId == HmacHash(payload.Subject));
            if(user is not null){     
                Console.WriteLine("UTENTE ESISTE");     
                return Ok($"{_manager.GenIdToken(user)}@{_manager.GenAccessToken(user.HashedId!)}@{_manager.GenRefreshToken(user.HashedId!)}");
            }else{
                GoogleUserSignUp googleUser = new(payload.Subject, payload.GivenName, payload.FamilyName, payload.Email);
                if(payload.Picture != ""){
                    googleUser.Propic = payload.Picture;
                }
                Console.WriteLine("UTENTE NON ESISTE");
                return Unauthorized(_manager.GenGoogleSignUpToken(payload));
            }
        }
        return Forbid();
    }
    [HttpPost("oauthsignup")]
    public async Task<IActionResult> OuathSignup([FromBody] GoogleUser user, [FromHeader(Name = "Authentication")] string token){
        var isValid = _manager.ValidateGoogleSignUpToken(token);
        if(isValid.StatusCode == 200){
            User? registeredUser = _context.User.SingleOrDefault(u => u.HashedId == HmacHash(user.Sub!));
            if(registeredUser is not null){
                return BadRequest("User gia' presente");
            }else{
                var id  = Guid.NewGuid();
                User newUser = new(){
                    Id = id, 
                    Name = user.Name, 
                    Surname = user.Surname, 
                    City = user.City,
                    DateOfBirth = user.DateOfBirth,
                    Email = user.Email,
                    Password = BCrypt.Net.BCrypt.HashPassword(token),
                    HashedId = HmacHash(user.Sub!),
                    CreatedAt = DateTime.Now,
                    Verified = true
                };
                _context.Add(newUser);
                await _context.SaveChangesAsync();
                return Ok($"{_manager.GenIdToken(newUser)}@{_manager.GenAccessToken(newUser.HashedId!)}@{_manager.GenRefreshToken(newUser.HashedId!)}");;
            }
        }else{
            return StatusCode(isValid.StatusCode);
        }
       
    }

    [HttpPost]
    public async Task<IActionResult> PostSignup([FromBody] UserDTO GuestUser)
    {
        try{
            var user = _context.User.FirstOrDefault(user => user.Email == GuestUser.Email);
            if(user == null){
                Guid id = Guid.NewGuid();
                string HashedId = HmacHash(id.ToString());
                bool emailSent = SendEmail(GuestUser, HashedId);
                Console.WriteLine($"email sent: {emailSent}");
                if(emailSent == true){
                    User newUser = new();
                    newUser = newUser.FomUserDTO(GuestUser) ?? throw new DataNotValid("dati user errati");
                    newUser.Id = id;
                    newUser.HashedId = HashedId;                
                    _context.User.Add(newUser);
                    await _context.SaveChangesAsync();
                    return Ok("Nuovo Privato inserito!");
                }else{
                    return BadRequest("l'email non e' corretta");
                }
            }else{
                throw new UserNotFound("user gia' presente!"); 
            }   
        }
        catch(UserNotFound e){
            return BadRequest(e.Message);
        }catch(MailKit.Net.Smtp.SmtpCommandException e){
            return BadRequest(e.Message);
        }catch(DataNotValid e){
            return BadRequest(e.Message);
        }
    }


    private static GoogleJsonWebSignature.Payload? ValidateGoogleToken([FromForm] string idToken)
    {
        try
        {   
            GoogleJsonWebSignature.Payload payload = GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new List<string> { "746085633002-r4lsvaljlfdri7uvnio7l6l0g1ch51q2.apps.googleusercontent.com" }
            }).Result;
            return payload;
        }
      
        catch (Exception e)
        {
            Console.WriteLine(e);
            return null;
        }
    }


}
