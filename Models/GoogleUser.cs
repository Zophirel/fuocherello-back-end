using Newtonsoft.Json.Linq;
//calasses needed to let the user login / signup with google oauth 
namespace Fuocherello.Models{
    public class GoogleUser
    {

        public string? Sub { get; set; }
        public string? Name { get; set; }
        public string? Surname { get; set; }
        public string? City { get; set; }
        public string? Email { get; set; }
        public string? Propic { get; set; }
        public DateTime DateOfBirth { get; set; }

        public GoogleUser(){}

        private GoogleUser(string Sub, string Name, string Surname, string City, string Email, DateTime DateOfBirth){
            
            this.Sub = Sub;
            this.Name = Name;
            this.Surname = Surname;
            this.City = City;
            this.Email = Email;
            this.DateOfBirth = DateOfBirth;
        }

        public GoogleUser FromJson(string json){
            Console.WriteLine(json);
            JObject data = JObject.Parse(json);
            Sub = data["sub"]!.ToString();
            Name = data["nome"]!.ToString();
            Surname = data["cognome"]!.ToString();
            City = data["comune"]!.ToString();
            Email = data["email"]!.ToString();
            DateOfBirth = DateTime.Parse(data["data_nascita"]!.ToString());
            return this;
        }

      
    }

    public class GoogleUserSignUp
    {
        public GoogleUserSignUp(string Sub, string Name, string Surname, string Email){
            this.Sub = Sub;
            this.Name = Name;
            this.Surname = Surname;
            this.Email = Email;   
        }
        public string? Sub { get; set; }
        public string? Name { get; set; }
        public string? Surname { get; set; }
        public string? Email { get; set; }
        public string? Propic { get; set; }
      
    }
}