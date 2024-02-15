using Microsoft.EntityFrameworkCore;


namespace Fuocherello.Data
{
    public class ApiServerContext : DbContext
    {


        public ApiServerContext (DbContextOptions<ApiServerContext> options)
            : base(options)
        {
            
        }

        public DbSet<Models.User> Users { get; set; } = default!;
    
        public DbSet<Models.Product> Product { get; set; } = default!;

        public DbSet<Models.SavedProduct> SavedProduct { get; set; } = default!;
        
        public DbSet<Models.ChatList> ChatList { get; set; } = default!;
        
        public DbSet<Models.UserMessage> Message { get; set; } = default!;
        
        //filtered products 
        public DbSet<Models.Legname> Legname {get; set;} = default!;
        public DbSet<Models.Biomasse> Biomasse {get; set;} = default!;
        public DbSet<Models.Pellet> Pellet {get; set;} = default!;

    }
}
