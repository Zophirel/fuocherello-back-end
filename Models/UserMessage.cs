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


    public UserMessage fromJson(string json){
        Guid chat_id;
        Console.WriteLine(json);
        JObject message = JObject.Parse(json);
        if(message["chatId"] is not null || message["chatId"]!.ToString() == "" || message["chatId"]!.ToString() == "null"){
            chat_id = Guid.NewGuid(); 
        }else {
            chat_id = Guid.Parse(message["chatId"]!.ToString());
        }

        Id = Guid.NewGuid();
        ChatId = chat_id;
        ProdId = Guid.Parse(message["prodId"]!.ToString());
        SenderId = message["from"]!.ToString();
        Message = message["message"]!.ToString();
        SentAt = ulong.Parse(message["sentAt"]!.ToString());
        Delivered = false;
        return this;
    }

    public string toAppMessage(string jsonMessage){
        JObject data = JObject.Parse(jsonMessage);
        string begin = "{";
        string end = "}";
        string json = $""" "chatId" : "{ChatId}", "prodId" : "{ProdId}", "from" : "{SenderId}", "to" : "{data["to"]}", "message" : "{Message}", "sentAt" : "{SentAt}" """;
        json = begin + json.Substring(1, json.Length-1) + end;
        return json;
    }

}