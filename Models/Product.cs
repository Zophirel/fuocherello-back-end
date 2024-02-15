
using System.ComponentModel.DataAnnotations;

namespace Fuocherello.Models{

    public class Product {
    
        [Key]
        public Guid? Id { get; set; } = new Guid();
        public string? Author { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public double Price { get; set; }
        public List<string>? ProductImages { get; set; } = new List<string>();
        public string? Category { get; set; }
        public DateTime? CreatedAt { get; set; } = DateTime.Now;
        public string? Place { get; set; }
        public DateTime? UptadedAt { get; set; } = DateTime.Now;
        
    }
}