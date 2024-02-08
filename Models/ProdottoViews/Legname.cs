
using System.ComponentModel.DataAnnotations;

namespace final.Models{

    public class Legname {
    
        [Key]
        public Guid? id { get; set; } = new Guid();
        public string? autore { get; set; }
        public string? titolo { get; set; }
        public string? descrizione { get; set; }
        public double prezzo { get; set; }
        public List<string>? immagini_prodotto { get; set; } = new List<string>();
        public string? categoria { get; set; }
        public DateTime? created_at { get; set; } = DateTime.Now;
        public string? luogo_di_pubblicazione { get; set; }
        public DateTime? ultima_modifica { get; set; } = DateTime.Now;
        public string? tipo_autore {get; set; } = "utente";
        
    }
}