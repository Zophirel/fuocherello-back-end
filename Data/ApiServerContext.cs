using Microsoft.EntityFrameworkCore;


namespace Fuocherello.Data
{
    public class ApiServerContext : DbContext
    {


        public ApiServerContext (DbContextOptions<ApiServerContext> options)
            : base(options)
        {
            
        }

        public DbSet<Fuocherello.Models.Utente> utente { get; set; } = default!;
    
        public DbSet<Fuocherello.Models.Prodotto> prodotto { get; set; } = default!;

        public DbSet<Fuocherello.Models.Preferito> preferito { get; set; } = default!;

        public DbSet<Fuocherello.Models.UtenteKeys> utente_keys { get; set; } = default!;
        
        public DbSet<Fuocherello.Models.ChatList> lista_chat { get; set; } = default!;
        
        public DbSet<Fuocherello.Models.UserMessage> messaggio { get; set; } = default!;
        
        //filtered products 
        public DbSet<Fuocherello.Models.Legname> Legname {get; set;} = default!;
        public DbSet<Fuocherello.Models.Biomasse> Biomasse {get; set;} = default!;
        public DbSet<Fuocherello.Models.Pellet> Pellet {get; set;} = default!;

    }
}
