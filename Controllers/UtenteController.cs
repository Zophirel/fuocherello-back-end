using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Fuocherello.Data;
using Fuocherello.Singleton.JwtManager;
using Fuocherello.Models;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NpgsqlTypes;
using System.Text;
using System.Text.Json;
namespace Fuocherello.Controllers;

[ApiController]
[Route("api/[controller]")]

public class UserController : ControllerBase
{
    private readonly NpgsqlDataSource _conn;
    private readonly ApiServerContext _context;
    private readonly IJwtManager? _manager;
    private readonly IConfiguration _configuration;
    private readonly AmazonS3Client s3Client;
    public UserController( NpgsqlDataSource conn, ApiServerContext context,  IConfiguration configuration, IJwtManager manager)
    {
        _conn = conn;
        _context = context;
        _manager = manager;
        _configuration = configuration;
        string awsAccessKeyId = _configuration.GetValue<string>("S3awsAccessKeyId")!;
        string awsSecretAccessKey = _configuration.GetValue<string>("S3awsSecretAccessKey")!;
        AmazonS3Config config = new()
        {
            ServiceURL = "https://s3.cubbit.eu",
            ForcePathStyle = true
        };
        
        AWSCredentials creds = new BasicAWSCredentials(awsAccessKeyId, awsSecretAccessKey);
        s3Client = new AmazonS3Client(creds, config);
    }


    // GET: api/Fuocherello.Models.User/5
    [HttpGet("{id}")]
    public ActionResult GetUser(string id)
    {
        var user = _context.Users.SingleOrDefault(user => user.HashedId == id);
        Console.WriteLine($"USER == NULL : {user == null}");
        List<Dictionary<string, object>> formattedData = new();

        if(user != null)
        {
            Dictionary<string, object> data = new()
            {
                { "id", user.HashedId! },
                { "nome", user.Name! },
                { "cognome", user.Surname! },
                { "propic", user.Propic == "" ? false : true }
            };
            
            formattedData.Add(data);
        }
        var json = JsonSerializer.Serialize(formattedData);
        return Ok(json);
    }

    [HttpGet("messaggi/latest")]
    //return all the messages that a user couldn't receive while logged but disconnected from the signalr server
    public async Task<ActionResult> GetNotReceivedMessagges([FromHeader(Name = "Authentication")]string token){
        var isValid = _manager!.ValidateAccessToken(token);
        if(isValid.StatusCode == 200){
            string sub = _manager.ExtractSub(token)!;
            string query =  
            $""" 
            WITH updated_messages AS (
                UPDATE public.message AS msg
                SET delivered = true
                FROM (
                    SELECT id AS chat_id, buyer_id, seller_id
                    FROM public.chat_list
                    WHERE buyer_id = @id OR seller_id = @id
                ) AS chat
                WHERE msg.chat_id = chat.chat_id
                AND msg.delivered = false 
                AND msg.sender_id != @id
                RETURNING
                    msg.id,
                    msg.chat_id,
                    msg.sender_id,
                    msg.prod_id,
                    msg.message,
                    msg.sent_at,
                    msg.delivered
            )
            SELECT * FROM updated_messages;
            """; 
            
            await using var getContactQuery = _conn.CreateCommand(query);
            getContactQuery.Parameters.Add("@id", NpgsqlDbType.Text).Value = sub;
            await using var reader = await getContactQuery.ExecuteReaderAsync();
            
            List<Dictionary<string, string>> formattedData = new();            
            while (await reader.ReadAsync())
            {
                Dictionary<string, string> data = new()
                {
                    { "id", reader.GetGuid(0).ToString() },
                    { "chatId", reader.GetGuid(1).ToString() },
                    { "from", reader.GetString(2) },
                    { "prodId", reader.GetGuid(3).ToString() },
                    { "message", reader.GetString(4) },
                    { "sentAt", reader.GetInt64(5).ToString() }
                };
                formattedData.Add(data);
            }
            var json = JsonSerializer.Serialize(formattedData);
            return Ok(json);
        } 
        return StatusCode(isValid.StatusCode);
    }



    [HttpGet("messaggi")]
    //get all the user chat messages 
    public async Task<ActionResult> GetMessaggi([FromHeader(Name = "Authentication")]string token){
        var isValid = _manager!.ValidateAccessToken(token);
         if(isValid.StatusCode == 200){
            string sub = _manager.ExtractSub(token)!;
            string query =  
            $""" 
            SELECT
                msg.id,
                msg.chat_id,
                msg.sender_id,
                CASE 
                    WHEN msg.sender_id = chat.buyer_id THEN chat.seller_id
                    ELSE chat.buyer_id
                END AS receiver_id,
                msg.message,
                msg.sent_at,
                msg.delivered
            FROM public.message AS msg
            JOIN (
                SELECT id AS chat_id, buyer_id, seller_id
                FROM public.chat_list
                WHERE buyer_id = @id
                OR seller_id = @id
            ) AS chat ON msg.chat_id = chat.chat_id;
            """; 

            await using var getMessagesQuery = _conn.CreateCommand(query);
            getMessagesQuery.Parameters.Add("@id", NpgsqlDbType.Text).Value = sub;
            await using var reader = await getMessagesQuery.ExecuteReaderAsync();
            List<Dictionary<string, string>> formattedData = new();
            UTF8Encoding utf8 = new();
            
            while (await reader.ReadAsync())
            {
                Dictionary<string, string> data = new()
                {
                    { "id", reader.GetGuid(0).ToString() },
                    { "chat_id", reader.GetGuid(1).ToString() },
                    { "sender_id", reader.GetString(2) },
                    { "receiver_id", reader.GetString(3) },
                    { "message", reader.GetString(4) },
                    { "sent_at", reader.GetInt64(5).ToString() },
                    { "not_read_message", "0" }
                };
                formattedData.Add(data);
            }

            if(formattedData.Count == 0){
                return NoContent();
            }else{
                string json = JsonSerializer.Serialize(formattedData);
                return Ok(json);
            }
        } 
        return StatusCode(isValid.StatusCode);
    }

    [HttpGet("chat")]
    public async Task<ActionResult> GetChats([FromHeader(Name = "Authentication")]string token){
        var isValid = _manager!.ValidateAccessToken(token);
        if(isValid.StatusCode == 200){
            string sub = _manager.ExtractSub(token)!;
            string query =  
            $""" 
            SELECT
                CASE
                    WHEN buyer_id = @id THEN seller.hashed_id
                    ELSE buyer.hashed_id
                END AS user_hashed_id,
                product.title, 
                product.id AS prod_id,
                chat.id AS chat_id,
                product.product_images
                
            FROM public.chat_list AS chat
            JOIN public.product AS product ON chat.prod_id = product.id
            LEFT JOIN public.users AS buyer ON chat.buyer_id = buyer.hashed_id
            LEFT JOIN public.users AS seller ON chat.seller_id = seller.hashed_id
            WHERE chat.buyer_id = @id OR chat.seller_id = @id;
            """; 

            await using var getContactQuery = _conn.CreateCommand(query);
            getContactQuery.Parameters.Add("@id", NpgsqlDbType.Text).Value = sub;
            await using var reader = await getContactQuery.ExecuteReaderAsync();
            List<Dictionary<string, string>> formattedData = new();
            UTF8Encoding utf8 = new();
            while (await reader.ReadAsync())
            {
                Dictionary<string, string> data = new()
                {
                    { "id", reader.GetGuid(3).ToString() },
                    { "prod_id", reader.GetGuid(2).ToString() },
                    { "prod_name", reader.GetString(1) },
                    { "contact_id", reader.GetString(0) },
                    { "not_read_message", "0" },
                    { "thumbnail", ((string[])reader.GetValue(4))[0] }
                };
                formattedData.Add(data);
            }
            var json = JsonSerializer.Serialize(formattedData);

            return Ok(json);
        }
        return StatusCode(isValid.StatusCode);
    }

    [HttpGet("contatti")]
    public async Task<ActionResult> GetContacts([FromHeader(Name = "Authentication")]string token){
        var isValid = _manager!.ValidateAccessToken(token);
         if(isValid.StatusCode == 200){
            string sub = _manager.ExtractSub(token)!;
            string query =  
            $""" 
                SELECT DISTINCT
                    CASE
                        WHEN buyer.hashed_id = @id THEN seller.hashed_id
                        ELSE buyer.hashed_id
                    END AS user_hashed_id,
                    CASE
                        WHEN buyer.hashed_id = @id THEN seller.name
                        ELSE buyer.name
                    END AS user_name
                FROM public.chat_list AS chat
                LEFT JOIN public.users AS buyer ON chat.buyer_id = buyer.hashed_id
                LEFT JOIN public.users AS seller ON chat.seller_id = seller.hashed_id
                WHERE chat.buyer_id = @id OR chat.seller_id = @id;
            """; 

            await using var getContactQuery = _conn.CreateCommand(query);
            getContactQuery.Parameters.Add("@id", NpgsqlDbType.Text).Value = sub;
            await using var reader = await getContactQuery.ExecuteReaderAsync();
            List<Dictionary<string, string>> formattedData = new();
            UTF8Encoding utf8 = new();
            while (await reader.ReadAsync())
            {
                Dictionary<string, string> data = new()
                {
                    { "contact_id", reader.GetString(0) },
                    { "contact_name", reader.GetString(1) }
                };
                formattedData.Add(data);
            }
            var json = JsonSerializer.Serialize(formattedData);
            return Ok(json);
        }
        return StatusCode(isValid.StatusCode);
    }
    
    [HttpPut("info")]
    public async Task<ActionResult> PutUserInfo([FromHeader(Name = "Authentication")] string token, [FromForm] EditUserForm form, IFormFile? file){
        var isValid = _manager!.ValidateAccessToken(token);
        if(isValid.StatusCode == 200){
            string sub = _manager.ExtractSub(token)!;
            
            User user =_context.Users.Single(user => user.HashedId == sub);
            user.Name = form.Name ?? user.Name;
            user.Surname = form.Surname ?? user.Surname;
            user.DateOfBirth = form.DateOfBirth ?? user.DateOfBirth;
            user.City = form.City ?? user.City;  

            Console.WriteLine($"{user.Name} - {user.Surname} - {user.DateOfBirth} {user.City}");      
            if(file == null){
                _context.SaveChanges();
                return Ok();
            }else{
                var filename = sub + ".jpeg";
                var imgPath = $"profiles/{sub}/{filename}";
                PutObjectRequest request = new()
                {
                    BucketName = "fuocherello-bucket",
                    Key = imgPath,
                    InputStream = file.OpenReadStream(),
                    ContentType = file.ContentType,
                    UseChunkEncoding = false,
                    CannedACL = S3CannedACL.PublicRead
                };
                try{
                    // upload image to cloud
                    PutObjectResponse response = await s3Client.PutObjectAsync(request);
                    if(response.HttpStatusCode == System.Net.HttpStatusCode.Accepted){
                        user.Propic = $"https://fuocherello-bucket.s3.cubbit.eu/profiles/{sub}/{sub}.jpeg";
                        _context.SaveChanges();
                        return Ok();
                    }
                }catch(AmazonS3Exception ex){
                    Console.WriteLine($"Error: '{ex.Message}' when writing an object");  
                }
            }
        } 
        return StatusCode(isValid.StatusCode);
    }
}

