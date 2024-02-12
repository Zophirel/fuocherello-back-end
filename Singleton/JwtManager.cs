using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Fuocherello.Models;
using Google.Apis.Auth;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;

<<<<<<< HEAD:Singleton/JwtManager.cs
namespace Fuocherello.Singleton.JwtManager
=======
namespace Fuocherello.Models
>>>>>>> 436ad41e4c626c1f349b08b1359b18a7cb06a3e9:Models/JwtManager.cs
{
    public class MyStatusCodeResult{

        public MyStatusCodeResult(int statusCode, string? result = null){
            StatusCode = statusCode;
            Result = result;
        }

        public string? Result {get;}
        public int StatusCode {get;}
    }
    
    public class JwtManager : IJwtManager
    { 
        static readonly char[] padding = { '=' };
        private readonly JwtSecurityTokenHandler _tokenHandler = new();
        private readonly TokenValidationParameters? _validationParameters;
        private readonly JwtHeader? _header;

        // Private constructor to prevent instantiation from outside
        public JwtManager(RSA key)
        {
            
            var securityKey = new RsaSecurityKey(key);
            _validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = false,
                ValidIssuer = "https://www.zophirel.it:8443",
                IssuerSigningKey = securityKey
            };

            _header = new JwtHeader(new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256))
            {
                { "enc", "A256CBC-HS512" }
            };

            RSA rsaKey = RSA.Create();
            rsaKey.ImportRSAPrivateKey(File.ReadAllBytes("key"), out _);
        }


        private static bool ValidateLifetTime(ClaimsPrincipal payload){
            string? expStringValue = payload.Claims.FirstOrDefault(c => c.Type == "exp")?.Value;
            if(expStringValue is not null){
                DateTime exp = DateTimeOffset.FromUnixTimeSeconds(long.Parse(expStringValue)).DateTime;
                if(DateTime.UtcNow > exp){
                    return false;
                }else{
                    return true;
                }
            }
            return false;
        }
        
        public string Encode(string token){
           return Convert.ToBase64String(Encoding.ASCII.GetBytes(token))
                        .TrimEnd(padding).Replace('+', '-').Replace('/', '_');
        }

        public string Decode(string encodedToken){
            string incoming = encodedToken.Replace('_', '/').Replace('-', '+');
            switch(encodedToken.Length % 4) {
                case 2: incoming += "=="; break;
                case 3: incoming += "="; break;
            }
            byte[] bytes = Convert.FromBase64String(incoming);
            return Encoding.ASCII.GetString(bytes);
        }

        static string readASJson(string token){
            var jwtHandler = new JwtSecurityTokenHandler();
            var jwt = jwtHandler.ReadJwtToken(token);
            return jwt.Payload.SerializeToJson();
        }

        public string? ExtractSub(string token){
 
            JObject payload = JObject.Parse(readASJson(token));
            if(payload["sub"] != null){
                return payload["sub"]!.ToString();
            }else{
                return null;
            }
        } 

        public string? ExtractType(string token){
 
            JObject payload = JObject.Parse(readASJson(token));
            if(payload["type"] != null){
                return payload["type"]!.ToString();
            }else{
                return null;
            }
        } 
 
        public string? ExtractRole(string token){
            JObject payload = JObject.Parse(readASJson(token));
            if(payload["role"] != null){
                return payload["role"]!.ToString();
            }else{
                return null;
            }

        } 
       
        public string GenIdToken(Utente user){
            DateTime data = user.data_nascita!.Value;
            var DataNascita = $"{data.Year}-{data.Month}-{data.Day}";
            var claims = new []{
                new Claim("sub", user.hashed_id!),
                new Claim("Nome", user.nome!),
                new Claim("Cognome", user.cognome!),
                new Claim("Comune", user.comune!),
                new Claim("Email", user.email!),
                new Claim("DataNascita", DataNascita),
                //new Claim("ChatKey", _context.utente_keys.First(key => key.user_id == user.hashed_id).private_key!)
            };

            var payload = new JwtPayload("https://www.zophirel.it:8443", "", claims, DateTime.UtcNow, DateTime.UtcNow.AddMilliseconds(1), DateTime.UtcNow);
            var jweToken = new JwtSecurityToken(_header, payload);

            var tokenHandler = new JwtSecurityTokenHandler();
            return tokenHandler.WriteToken(jweToken);
        }
   
        public string GenAccessToken(string id){
            var claims = new[]
            {
                new Claim("sub", id!),
                new Claim("role", "utente"),
                new Claim("type", "Access")
                // Add additional claims as needed
            };

            var payload = new JwtPayload("https://www.zophirel.it:8443", "", claims, DateTime.UtcNow, DateTime.UtcNow.AddDays(3), DateTime.UtcNow);
            var jweToken = new JwtSecurityToken(_header, payload);

            var tokenHandler = new JwtSecurityTokenHandler();
            return tokenHandler.WriteToken(jweToken);
        }

        public string GenRefreshToken(string id){
            
            var claims = new[]{
                new Claim("sub", id),
                new Claim("role", "utente"),
                new Claim("type", "Refresh")
            };
        
            var payload = new JwtPayload("https://www.zophirel.it:8443", "", claims, DateTime.UtcNow, DateTime.UtcNow.AddDays(3), DateTime.UtcNow);
            var jweToken = new JwtSecurityToken(_header, payload);

            var tokenHandler = new JwtSecurityTokenHandler();
            return tokenHandler.WriteToken(jweToken);
        }

        public string GenEmailVerifyToken(string id){
            var claims = new[]
            {
                new Claim("sub", id),
                new Claim("type", "VerifyEmail")
            };


            var payload = new JwtPayload("https://www.zophirel.it:8443", "", claims, DateTime.UtcNow, DateTime.UtcNow.AddHours(2), DateTime.UtcNow);
            var jweToken = new JwtSecurityToken(_header, payload);

            var tokenHandler = new JwtSecurityTokenHandler();
            return tokenHandler.WriteToken(jweToken);
        }

        public string GenChangePassToken(string id){
            var claims = new[]
            {
                new Claim("sub", id),
                new Claim("type", "ChangePass")
            };
 

            var payload = new JwtPayload("https://www.zophirel.it:8443", "", claims, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(30), DateTime.UtcNow);
            var jweToken = new JwtSecurityToken(_header, payload);

            var tokenHandler = new JwtSecurityTokenHandler();
            return tokenHandler.WriteToken(jweToken);
        }

        public string GenGoogleSignUpToken(GoogleJsonWebSignature.Payload idToken){
            var claims = new[]
            {
                new Claim("sub", idToken.Subject),
                new Claim("role", "utente"),
                new Claim("type", "GoogleSignup"),
                new Claim("nome", idToken.GivenName),
                new Claim("cognome", idToken.FamilyName),
                new Claim("email", idToken.Email)
            };
 

            var payload = new JwtPayload("https://www.zophirel.it:8443", "", claims, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(31), DateTime.UtcNow);
            var jweToken = new JwtSecurityToken(_header, payload);

            var tokenHandler = new JwtSecurityTokenHandler();
            return tokenHandler.WriteToken(jweToken);
        }

        public MyStatusCodeResult ValidateAccessToken(string jwt){
            var payload = _tokenHandler.ValidateToken(jwt, _validationParameters, out _);
            if(payload is not null && ExtractType(jwt) == "Access"){
                if(ValidateLifetTime(payload)){
                    //Access Token valido
                    return new MyStatusCodeResult(200);
                }else{
                    //Access Token scaduto, si chide al client di richiamare verificando il refresh token
                    return new MyStatusCodeResult(401);
                }
            }
            //Token non valido
            return new MyStatusCodeResult(403);
        }
        
        public MyStatusCodeResult ValidateRefreshToken(string jwt){
            var payload = _tokenHandler.ValidateToken(jwt, _validationParameters, out _);
            if(payload is not null && ExtractType(jwt) == "Refresh"){
                if(ValidateLifetTime(payload)){
                    string? sub = ExtractSub(jwt);
                    //Refresh Token valido
                    if(sub is not null){
                        return new MyStatusCodeResult(200, GenAccessToken(sub));
                    }
                }
            }
            //Token non valido
            return new MyStatusCodeResult(403);
        }

        public MyStatusCodeResult ValidateVerifyEmailToken(string jwt){
            var payload = _tokenHandler.ValidateToken(jwt, _validationParameters, out _);
            if(payload is not null && ExtractType(jwt) == "VerifyEmail"){
                if(ValidateLifetTime(payload)){
                    //Refresh Token valido
                    return new MyStatusCodeResult(200);
                }
            }
            //Token non valido
            return new MyStatusCodeResult(403);
        }

        public MyStatusCodeResult ValidateRecoverPassToken(string jwt){
            var payload = _tokenHandler.ValidateToken(jwt, _validationParameters, out _);
            if(payload is not null){
                if(ExtractType(jwt) == "ChangePass"){
                    if(ValidateLifetTime(payload)){
                        //Refresh Token valido
                        return new MyStatusCodeResult(200);
                    }else{
                        Console.WriteLine("expired");
                    }
                }
            }else{
                Console.WriteLine("token non valido");
            }
            //Token non valido
            return new MyStatusCodeResult(403);
        }

        public MyStatusCodeResult ValidateGoogleSignUpToken(string jwt){
            var payload = _tokenHandler.ValidateToken(jwt, _validationParameters, out _);
            if(payload is not null && ExtractType(jwt) == "GoogleSignup"){
                if(ValidateLifetTime(payload)){
                    //Refresh Token valido
                    return new MyStatusCodeResult(200);
                }
            }
            //Token non valido
            return new MyStatusCodeResult(403);
        }
    }

}

