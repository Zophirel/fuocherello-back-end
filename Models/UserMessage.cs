using Newtonsoft.Json.Linq;

namespace final.Models;
public class UserMessage{
    public  Guid? id {get; set;}
    public  Guid? chat_id {get; set;}
    public  Guid? prod_id {get; set;}
    public string? mandante_id {get; set;}
    public string? messaggio {get; set;}
    public  ulong? sent_at {get; set;}
    public  bool? delivered {get; set;}

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

        id = Guid.NewGuid();
        this.chat_id = chat_id;
        prod_id = Guid.Parse(message["prodId"]!.ToString());
        mandante_id = message["from"]!.ToString();
        messaggio = message["message"]!.ToString();
        sent_at = ulong.Parse(message["sentAt"]!.ToString());
        delivered = false;
        return this;
    }

    public string toAppMessage(string jsonMessage){
        JObject data = JObject.Parse(jsonMessage);
        string begin = "{";
        string end = "}";
        string json = $""" "chatId" : "{chat_id}", "prodId" : "{prod_id}", "from" : "{mandante_id}", "to" : "{data["to"]}", "message" : "{messaggio}", "sentAt" : "{sent_at}" """;
        json = begin + json.Substring(1, json.Length-1) + end;
        return json;
    }

}