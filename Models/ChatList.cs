using System.ComponentModel.DataAnnotations;

namespace Fuocherello.Models;
public class ChatList{

    public ChatList(){
        id = Guid.NewGuid();
    }
    public ChatList(Guid prod_id, string buyer_id, string seller_id)
    {
        
        id = Guid.NewGuid();
        this.prod_id = prod_id;
        compratore_id = buyer_id;
        venditore_id = seller_id;
    }

    [Key]
    public Guid id {get; set;}
    public Guid prod_id {get; set;}
    public string? compratore_id {get; set;}
    public string? venditore_id {get; set;}

}