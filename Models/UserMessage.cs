using Newtonsoft.Json.Linq;

namespace Fuocherello.Models;
public class UserMessage{
    public  Guid? Id {get; set;}
    public  Guid? ChatId {get; set;}
    public  Guid? ProdId {get; set;}
    public string? SenderId {get; set;}
    public string? Message {get; set;}
    public  ulong? SentAt {get; set;}
    public  bool? Delivered {get; set;}

    public UserMessage(){}

}