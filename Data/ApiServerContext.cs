using Microsoft.EntityFrameworkCore;


namespace final.Data
{
    public class ApiServerContext : DbContext
    {


        public ApiServerContext (DbContextOptions<ApiServerContext> options)
            : base(options)
        {
            
        }

        public DbSet<final.Models.Utente> utente { get; set; } = default!;
    
        public DbSet<final.Models.Prodotto> prodotto { get; set; } = default!;

        public DbSet<final.Models.Preferito> preferito { get; set; } = default!;

        public DbSet<final.Models.UtenteKeys> utente_keys { get; set; } = default!;
        
        public DbSet<final.Models.ChatList> lista_chat { get; set; } = default!;
        
        public DbSet<final.Models.UserMessage> messaggio { get; set; } = default!;
        
        //filtered products 
        public DbSet<final.Models.Legname> Legname {get; set;} = default!;
        public DbSet<final.Models.Biomasse> Biomasse {get; set;} = default!;
        public DbSet<final.Models.Pellet> Pellet {get; set;} = default!;

    }
}
