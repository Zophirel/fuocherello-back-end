using System.ComponentModel.DataAnnotations;

namespace final.Models{
    public class UtenteKeys {
    
        [Key]
        public required string user_id {get; set;}
        public required string public_key {get; set;}
        public required string private_key {get; set;}
    }
}