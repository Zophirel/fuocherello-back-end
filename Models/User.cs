using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Fuocherello.Models
{
    //is not 'User' cause PostgreSql use User as keyword 
    public class User
    {
        public User(){
            
        }

        private static bool CheckIfElementExist(DateTime? element){
            if(element != null){
                if(element.Value.Year < DateTime.Now.Year - 13){
                    return true;
                } 
            }
            return false;
        }

        private static bool ValidatePassword(string? value)
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

        private static bool CheckIfNameExist(string? element){
            if(element != null){
                if(element != "" && element.Length < 15){
                    return true;
                }
            }
            return false;
        }

        private static bool CheckIfElementExist(string? element){
            if(element != null && element != ""){
                return true;      
            }
            return false;
        }

        private static string? FixNameString(string name)
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


        public User? FomUserDTO(UserDTO user){
            try{   
                if(!CheckIfNameExist(user.Name)){
                    throw new Exception("Nome non presente");
                }else{
                    Name = FixNameString(user.Name!);
                }

                if(!CheckIfNameExist(user.Surname)){
                    throw new Exception("Cognome non presente");
                }else{
                    Surname = FixNameString(user.Surname!);
                }
                
                if(!CheckIfElementExist(user.DateOfBirth)){
                    throw new Exception("Data di nascita errata");
                }else{
                    DateOfBirth = user.DateOfBirth;
                }

                if(!CheckIfElementExist(user.City)){
                    throw new Exception("Comune non presente");
                }else{
                    City = user.City;
                }

                if(!CheckIfElementExist(user.Email)){
                    throw new Exception("Email non presente");
                }else{
                    Email = user.Email;
                }

                if(!ValidatePassword(user.Password)){
                    throw new Exception("Password earrata o non presente");
                }else{
                    Password = BCrypt.Net.BCrypt.HashPassword(user.Password!);
                }
                CreatedAt = DateTime.Now;
                Verified = false;
                return this;
            }catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }

        [Key]
        public Guid? Id { get; set; }
        public string? Name { get; set; }
        public string? Surname { get; set; }
        public string? City { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public DateTime? CreatedAt {get; set; }
        public bool Verified {get; set;}
        public string? HashedId { get; set; }
        public string? Propic { get; set; } = "";  
    }
}