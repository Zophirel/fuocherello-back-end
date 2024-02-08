using Microsoft.AspNetCore.Mvc;
using final.Models;
using final.Data;
using Npgsql;
using final.Services.EmailService;
using System.Text;
using System.Security.Cryptography;
using Google.Apis.Auth;

namespace final.Controllers;

[ApiController]
[Route("/signup/privato")]

public class SignUpController : ControllerBase
{
    static readonly char[] padding = { '=' };
    private readonly NpgsqlDataSource _conn;
    private readonly ApiServerContext _context;
    private readonly IConfiguration _configuration;
    private readonly RSA _rsa;
    private readonly JwtManager  _manager;
    public SignUpController(ApiServerContext context, NpgsqlDataSource conn, IConfiguration configuration, RSA rsa)
    {
        _context = context;
        _conn = conn;
        _configuration = configuration;
        _rsa = rsa;
        _manager = JwtManager.GetInstance(_rsa);

    }

    //GET per eseguire il lancio in app tramite deeplink 
    [HttpGet("redirect")]
    public IActionResult TriggerDeepLink([FromQuery] string url){
        string[] splittedUrl = url.Substring(13).Split('/');
        string encodedToken = splittedUrl.Last();
        string pathParameter = splittedUrl[1]; 
        
        //redirect api force the server to serve only link inside the app
        url = $"fuocherello://app/{pathParameter}/{encodedToken}";
        return new RedirectResult(url);
    }
    
    private string HmacHash(string id)
    {
        string? secretKey = _configuration.GetValue<string>("SecretKey");
        string hash = "";
        if(secretKey != null){
            byte[] secretKeyBytes = Encoding.UTF8.GetBytes(secretKey);
            byte[] messageBytes = Encoding.UTF8.GetBytes(id.ToString());
            
            using (HMACSHA256 hmac = new(secretKeyBytes))
            {
                byte[] hashBytes = hmac.ComputeHash(messageBytes);
                hash = Convert.ToBase64String(hashBytes);
                string url_safe_id = _manager.encode(hash);

                Console.WriteLine("HMAC hash: " + hash);
                return url_safe_id;
            }
        }
        return "";
    }

    private bool SendEmail(UtenteDto GuestUser, string id){
    try{
        if(GuestUser.email != null){
            var service = new EmailService(_configuration);
            string verifyToken = _manager.GenEmailVerifyToken(id);
            Console.WriteLine(verifyToken);
            Console.WriteLine($"VERIFY EMAIL TOKEN IS VALID ON GEN {_manager.ValidateVerifyEmailToken(verifyToken).statusCode}");
            string encodedToken = System.Convert.ToBase64String(Encoding.ASCII.GetBytes(verifyToken))
            .TrimEnd(padding).Replace('+', '-').Replace('/', '_');
            
            string verifyLink = $"https://www.zophirel.it:8443/signup/privato/redirect?url=fuocherello://verifyemail/{encodedToken}";

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
                    To = GuestUser.email
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

    [HttpPost("oauth")]
    public ActionResult OuathLogin([FromForm] string idToken){
        
        GoogleJsonWebSignature.Payload payload = GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = new List<string> { "746085633002-r4lsvaljlfdri7uvnio7l6l0g1ch51q2.apps.googleusercontent.com" }
        }).Result;

        Console.WriteLine($"PAYLOAD NULL {payload is not null}");
        if(payload != null){
            Utente? user = _context.utente.SingleOrDefault(user => user.hashed_id == HmacHash(payload.Subject));
            if(user is not null){     
                Console.WriteLine("UTENTE ESISTE");     
                return Ok($"{_manager.GenIdToken(user)}@{_manager.GenAccessToken(user.hashed_id!)}@{_manager.GenRefreshToken(user.hashed_id!)}");
            }else{
                UtenteGoogleSignUp googleUser = new(payload.Subject, payload.GivenName, payload.FamilyName, payload.Email);
                if(payload.Picture != ""){
                    googleUser.propic = payload.Picture;
                }
                Console.WriteLine("UTENTE NON ESISTE");
                return Unauthorized(_manager.GenGoogleSignUpToken(payload));
            }
        }
        return Forbid();
    }
    [HttpPost("oauth/signup")]
    public async Task<IActionResult> OuathSignup([FromBody] UtenteGoogle user, [FromHeader(Name = "Authentication")] string token){
        var isValid = _manager.ValidateGoogleSignUpToken(token);
        if(isValid.statusCode == 200){
            Utente? registeredUser = _context.utente.SingleOrDefault(u => u.hashed_id == HmacHash(user.sub!));
            if(registeredUser is not null){
                return BadRequest("Utente gia' presente");
            }else{
                var id  = Guid.NewGuid();
                Utente newUser = new(){
                    id = id, 
                    nome = user.nome, 
                    cognome = user.cognome, 
                    comune = user.comune,
                    data_nascita = user.data_nascita,
                    email = user.email,
                    password = BCrypt.Net.BCrypt.HashPassword(token),
                    hashed_id = HmacHash(user.sub!),
                    created_at = DateTime.Now,
                    verified = true
                };
                _context.Add(newUser);
                await _context.SaveChangesAsync();
                return Ok($"{_manager.GenIdToken(newUser)}@{_manager.GenAccessToken(newUser.hashed_id!)}@{_manager.GenRefreshToken(newUser.hashed_id!)}");;
            }
        }else{
            return StatusCode(isValid.statusCode);
        }
       
    }

    [HttpPost]
    public async Task<IActionResult> PostSignup([FromBody] UtenteDto GuestUser)
    {
        try{
            var user = _context.utente.FirstOrDefault(user => user.email == GuestUser.email);
            if(user == null){
                Guid id = Guid.NewGuid();
                string hashed_id = HmacHash(id.ToString());
                bool emailSent = SendEmail(GuestUser, hashed_id);
                Console.WriteLine($"email sent: {emailSent}");
                if(emailSent == true){
                    Utente newUser = new();
                    newUser = newUser.fromUserDto(GuestUser) ?? throw new DataNotValid("dati utente errati");
                    newUser.id = id;
                    newUser.hashed_id = hashed_id;                
                    _context.utente.Add(newUser);
                    
                    await _context.SaveChangesAsync();
                    return Ok("Nuovo Privato inserito!");
                }else{
                    return BadRequest("l'email non e' corretta");
                }
            }else{
                throw new UserNotFound("utente gia' presente!"); 
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
