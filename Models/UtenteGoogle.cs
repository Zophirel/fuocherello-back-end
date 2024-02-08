using Newtonsoft.Json.Linq;
//calasses needed to let the user login / signup with google oauth 
namespace final.Models{
    public class UtenteGoogle
    {

        public UtenteGoogle(){}
        UtenteGoogle(string Sub, string Nome, string Cognome, string Comune, string Eamil, DateTime Data_Nascita){
            
            sub = Sub;
            nome = Nome;
            cognome = Cognome;
            comune = Comune;
            email = Eamil;
            data_nascita = Data_Nascita;
        }

        public UtenteGoogle fromJson(string json){
            Console.WriteLine(json);
            JObject data = JObject.Parse(json);
            sub = data["sub"]!.ToString();
            nome = data["nome"]!.ToString();
            cognome = data["cognome"]!.ToString();
            comune = data["comune"]!.ToString();
            email = data["email"]!.ToString();
            data_nascita = DateTime.Parse(data["data_nascita"]!.ToString());
            return this;
        }
        public string? sub { get; set; }
        public string? nome { get; set; }
        public string? cognome { get; set; }
        public string? comune { get; set; }
        public string? email { get; set; }
        public string? propic { get; set; }

        public DateTime data_nascita { get; set; }
      
    }

    public class UtenteGoogleSignUp
    {
        public UtenteGoogleSignUp(string Sub, string Nome, string Cognome, string Eamil){
            sub = Sub;
            nome = Nome;
            cognome = Cognome;
            email = Eamil;   
        }
        public string? sub { get; set; }
        public string? nome { get; set; }
        public string? cognome { get; set; }
        public string? email { get; set; }
        public string? propic { get; set; }
      
    }
}