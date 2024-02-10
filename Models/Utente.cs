using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Fuocherello.Models
{
    public class Utente
    {
        public Utente(){
            
        }

        static bool CheckIfElementExist(DateTime? element){
            if(element != null){
                if(element.Value.Year < DateTime.Now.Year - 13){
                    return true;
                } 
            }
            return false;
        }

        static bool ValidatePassword(string? value)
        {
            Regex regex = new Regex(@"^(?=.*?[A-Z])(?=.*?[a-z])(?=.*?[0-9])(?=.*?[!@#\$&*~]).{8,}$");
            if (value == null)
            {
                return false;
            }
            else if(!regex.IsMatch(value))
            {
                return false;
            }else{
                return true;
            }
        }

        static bool checkIfNameExist(string? element){
            if(element != null){
                if(element != "" && element.Length < 15){
                    return true;
                }
            }
            return false;
        }

        static bool checkIfElementExist(string? element){
            if(element != null && element != ""){
                return true;      
            }
            return false;
        }

        static string? _fixNameString(string name)
        {
            string restOfTheName = "";
            if(!char.IsUpper(name[0])){
                char firstLetter = char.ToUpper(name[0]);
                restOfTheName = name.Substring(1).ToLower();
                return $"{firstLetter}{restOfTheName}"; 
            }                
            restOfTheName = name.Substring(1).ToLower();
            return $"{name[0]}{restOfTheName}";
        }


        public Utente? fromUserDto(UtenteDto newUser){
            try{   
                if(!checkIfNameExist(newUser.nome)){
                    throw new Exception("Nome non presente");
                }else{
                    nome = _fixNameString(newUser.nome!);
                }

                if(!checkIfNameExist(newUser.cognome)){
                    throw new Exception("Cognome non presente");
                }else{
                    cognome = _fixNameString(newUser.cognome!);
                }
                
                if(!CheckIfElementExist(newUser.data_nascita)){
                    throw new Exception("Data di nascita errata");
                }else{
                    data_nascita = newUser.data_nascita;
                }

                if(!checkIfElementExist(newUser.comune)){
                    throw new Exception("Comune non presente");
                }else{
                    comune = newUser.comune;
                }

                if(!checkIfElementExist(newUser.email)){
                    throw new Exception("Email non presente");
                }else{
                    email = newUser.email;
                }

                if(!ValidatePassword(newUser.password)){
                    throw new Exception("Password earrata o non presente");
                }else{
                    password = BCrypt.Net.BCrypt.HashPassword(newUser.password!);
                }
                created_at = DateTime.Now;
                verified = false;
                return this;
            }catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }

        [Key]
        public Guid? id { get; set; }
        public string? nome { get; set; }
        public string? cognome { get; set; }
        public string? comune { get; set; }
        public string? email { get; set; }
        public string? password { get; set; }
        public DateTime? data_nascita { get; set; }
        public DateTime? created_at {get; set; }
        public bool verified {get; set;}
        public string? hashed_id { get; set; }
        public string? propic { get; set; } = "";  
    }
}