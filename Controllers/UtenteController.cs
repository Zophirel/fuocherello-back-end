using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Fuocherello.Data;
using Fuocherello.Models;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NpgsqlTypes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace Fuocherello.Controllers;

[ApiController]
[Route("api/[controller]")]

public class UtenteController : ControllerBase
{
    private readonly NpgsqlDataSource _conn;
    private readonly ApiServerContext _context;
    private static JwtManager? _manager;
    private readonly IConfiguration _configuration;
    private readonly AmazonS3Client s3Client;
    public UtenteController( NpgsqlDataSource conn, ApiServerContext context, RSA key, IConfiguration configuration)
    {
        _conn = conn;
        _context = context;
        _manager = JwtManager.GetInstance(key);
        _configuration = configuration;
        string awsAccessKeyId = configuration.GetValue<string>("S3awsAccessKeyId")!;
        string awsSecretAccessKey = configuration.GetValue<string>("S3awsSecretAccessKey")!;
        AmazonS3Config config = new()
        {
            ServiceURL = "https://s3.cubbit.eu",
            ForcePathStyle = true
        };
        
        AWSCredentials creds = new BasicAWSCredentials(awsAccessKeyId, awsSecretAccessKey);
        s3Client = new AmazonS3Client(creds, config);
    }


    // GET: api/Fuocherello.Models.Utente/5
    [HttpGet("{id}")]
    public ActionResult GetUtente(string id)
    {
        var user = _context.utente.SingleOrDefault(user => user.hashed_id == id);
        Console.WriteLine($"USER == NULL : {user == null}");
        List<Dictionary<string, object>> formattedData = new();

        if(user != null)
        {
            Dictionary<string, object> data = new()
            {
                { "id", user.hashed_id! },
                { "nome", user.nome! },
                { "cognome", user.cognome! },
                { "propic", user.propic == "" ? false : true }
            };
            //data.Add("chat_pub_key", _context.utente_keys.Single( key => key.user_id == user.hashed_id)!.public_key);
            formattedData.Add(data);
        }
        var json = JsonSerializer.Serialize(formattedData);
        return Ok(json);
    }

    [HttpGet("messaggi/latest")]
    //return all the messages that a user couldn't receive while logged but disconnected from the signalr server
    public async Task<ActionResult> GetNotReceivedMessagges([FromHeader(Name = "Authentication")]string token){
        var isValid = _manager!.ValidateAccessToken(token);
        if(isValid.statusCode == 200){
            string sub = _manager.ExtractSub(token)!;
            string query =  
            $""" 
            WITH updated_messages AS (
                UPDATE public.messaggio AS msg
                SET delivered = true
                FROM (
                    SELECT id AS chat_id, compratore_id, venditore_id
                    FROM public.lista_chat
                    WHERE compratore_id = @id OR venditore_id = @id
                ) AS chat
                WHERE msg.chat_id = chat.chat_id
                AND msg.delivered = false 
                AND msg.mandante_id != @id
                RETURNING
                    msg.id,
                    msg.chat_id,
                    msg.mandante_id,
                    msg.prod_id,
                    msg.messaggio,
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
        return StatusCode(isValid.statusCode);
    }



    [HttpGet("messaggi")]
    //get all the user chat messages 
    public async Task<ActionResult> GetMessaggi([FromHeader(Name = "Authentication")]string token){
        var isValid = _manager!.ValidateAccessToken(token);
         if(isValid.statusCode == 200){
            string sub = _manager.ExtractSub(token)!;
            string query =  
            $""" 
            SELECT
                msg.id,
                msg.chat_id,
                msg.mandante_id,
                CASE 
                    WHEN msg.mandante_id = chat.compratore_id THEN chat.venditore_id
                    ELSE chat.compratore_id
                END AS ricevente_id,
                msg.messaggio,
                msg.sent_at,
                msg.delivered
            FROM public.messaggio AS msg
            JOIN (
                SELECT id AS chat_id, compratore_id, venditore_id
                FROM public.lista_chat
                WHERE compratore_id = @id
                OR venditore_id = @id
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
        return StatusCode(isValid.statusCode);
    }

    [HttpGet("chat")]
    public async Task<ActionResult> GetChats([FromHeader(Name = "Authentication")]string token){
        var isValid = _manager!.ValidateAccessToken(token);
        if(isValid.statusCode == 200){
            string sub = _manager.ExtractSub(token)!;
            string query =  
            $""" 
            SELECT
                CASE
                    WHEN compratore_id = @id THEN venditore.hashed_id
                    ELSE compratore.hashed_id
                END AS utente_hashed_id,
                prodotto.titolo, 
                prodotto.id AS prod_id,
                chat.id AS chat_id,
                prodotto.immagini_prodotto
                
            FROM public.lista_chat AS chat
            JOIN public.prodotto AS prodotto ON chat.prod_id = prodotto.id
            LEFT JOIN public.utente AS compratore ON chat.compratore_id = compratore.hashed_id
            LEFT JOIN public.utente AS venditore ON chat.venditore_id = venditore.hashed_id
            WHERE chat.compratore_id = @id OR chat.venditore_id = @id;
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
        return StatusCode(isValid.statusCode);
    }

    [HttpGet("contatti")]
    public async Task<ActionResult> GetContacts([FromHeader(Name = "Authentication")]string token){
        var isValid = _manager!.ValidateAccessToken(token);
         if(isValid.statusCode == 200){
            string sub = _manager.ExtractSub(token)!;
            string query =  
            $""" 
                SELECT DISTINCT
                    CASE
                        WHEN compratore.hashed_id = @id THEN venditore.hashed_id
                        ELSE compratore.hashed_id
                    END AS utente_hashed_id,
                    CASE
                        WHEN compratore.hashed_id = @id THEN venditore.nome
                        ELSE compratore.nome
                    END AS utente_nome
                FROM public.lista_chat AS chat
                LEFT JOIN public.utente AS compratore ON chat.compratore_id = compratore.hashed_id
                LEFT JOIN public.utente AS venditore ON chat.venditore_id = venditore.hashed_id
                WHERE chat.compratore_id = @id OR chat.venditore_id = @id;

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
        return StatusCode(isValid.statusCode);
    }
    
    [HttpPut("info")]
    public async Task<ActionResult> PutUtenteInfo([FromHeader(Name = "Authentication")] string token, [FromForm] EditUtenteForm form, IFormFile? file){
        var isValid = _manager!.ValidateAccessToken(token);
        if(isValid.statusCode == 200){
            string sub = _manager.ExtractSub(token)!;
            
            Utente user =_context.utente.Single(user => user.hashed_id == sub);
            user.nome = form.Nome ?? user.nome;
            user.cognome = form.Cognome ?? user.cognome;
            user.data_nascita = form.DataDiNascita ?? user.data_nascita;
            user.comune = form.Comune ?? user.comune;  

            Console.WriteLine($"{user.nome} - {user.cognome} - {user.data_nascita} {user.comune}");      
            if(file == null){
                _context.SaveChanges();
                return Ok();
            }else{
                var filename = sub + ".jpeg";
                var imgPath = $"profiles/{sub}/{filename}";
                PutObjectRequest request = new()
                {
                    BucketName = "Fuocherello-bucket",
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
                        user.propic = $"https://Fuocherello-bucket.s3.cubbit.eu/profiles/{sub}/{sub}.jpeg";
                        _context.SaveChanges();
                        return Ok();
                    }
                }catch(AmazonS3Exception ex){
                    Console.WriteLine($"Error: '{ex.Message}' when writing an object");  
                }
            }
        } 
        return StatusCode(isValid.statusCode);
    }
}

