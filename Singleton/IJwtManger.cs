using Fuocherello.Models;
using Fuocherello.Singleton.JwtManager;
using Google.Apis.Auth;
namespace Fuocherello.Singleton.JwtManager
{
    public interface IJwtManager
    {
        public string Encode(string token);
        public string Decode(string encodedToken);
        public string? ExtractSub(string token);
        public string? ExtractType(string token);
        public string? ExtractRole(string token);
        public string GenIdToken(Utente user);
        public string GenAccessToken(string id);
        public string GenRefreshToken(string id);
        public string GenEmailVerifyToken(string id);
        public string GenChangePassToken(string id);
        public string GenGoogleSignUpToken(GoogleJsonWebSignature.Payload idToken);
        public MyStatusCodeResult ValidateAccessToken(string jwt);
        public MyStatusCodeResult ValidateRefreshToken(string jwt);
        public MyStatusCodeResult ValidateVerifyEmailToken(string jwt);
        public MyStatusCodeResult ValidateRecoverPassToken(string jwt);
        public MyStatusCodeResult ValidateGoogleSignUpToken(string jwt);
    }
}
