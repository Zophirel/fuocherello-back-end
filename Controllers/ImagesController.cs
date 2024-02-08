using Microsoft.AspNetCore.Mvc;
using final.Models;
using System.Security.Cryptography;
using Npgsql;
using final.Data;
using NpgsqlTypes;
using System.Text;
using Amazon.S3;
using Amazon.Runtime;
using Amazon.S3.Model;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
namespace final.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImagesController : ControllerBase
{
    private readonly NpgsqlDataSource conn;
    private readonly ApiServerContext _context;
    private readonly JwtManager _manager;
    private readonly IConfiguration _configuration;
    private readonly IAmazonS3 s3Client;
    private readonly HubConnection signalRClient;

    public ImagesController(RSA key, NpgsqlDataSource conn, ApiServerContext context, IConfiguration configuration)
    {
        this.conn = conn;
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
        signalRClient = new HubConnectionBuilder()
                .WithUrl("https://www.zophirel.it:8444/chathub")
                .Build();
    }


    [HttpGet("ProdottoImages/{ProdId}")]
    public IActionResult GetProductImage(Guid ProdId){  
        Prodotto?prod =_context.prodotto.FirstOrDefault(prod => prod.id == ProdId);
        if(prod != null){
            if(prod.immagini_prodotto == null){
                return NoContent();    
            }
            List<string> s3imgsUrl = new();
            foreach(string filename in prod.immagini_prodotto){
                s3imgsUrl.Add($"https://fuocherello-bucket.s3.cubbit.eu/products/{prod.autore}/{prod.id}/{filename}");
            }
            return Ok(s3imgsUrl);
        }else{
            return NotFound();
        } 
    }

    [HttpGet("ProdottoThumbnail/{ProdId}")]
    public IActionResult GetProductThumbnailUrlFromBucket(Guid ProdId){
        Prodotto?prod =_context.prodotto.FirstOrDefault(prod => prod.id == ProdId);
        if(prod != null){
            if(prod.immagini_prodotto == null){
                return NoContent();    
            }
            return Ok($"https://fuocherello-bucket.s3.cubbit.eu/products/{prod.autore}/{prod.id}/{prod.immagini_prodotto[0]}");
        }else{
            return NotFound();
        }  
    
    }
    private string HmacHash(Guid? id)
    {
        string? secretKey = _configuration.GetValue<string>("SecretKey");
        string hash = "";
        if(secretKey != null && id != null){
            byte[] secretKeyBytes = Encoding.UTF8.GetBytes(secretKey);
            byte[] messageBytes = Encoding.UTF8.GetBytes(id.ToString()!);
            
            using (HMACSHA256 hmac = new(secretKeyBytes))
            {
                byte[] hashBytes = hmac.ComputeHash(messageBytes);
                hash = Convert.ToBase64String(hashBytes);
                string url_safe_id = _manager.encode(hash);

                Console.WriteLine("HMAC hash: " + hash);
                return url_safe_id;
            }
        }
        return "";
    }

    //upload images remotely using aws s3 api and cubbit as endpoint
    [HttpPost("Prodotto")]
    public async Task<ActionResult<Prodotto>> PostProdottoImagesToBucket([FromForm] List<IFormFile> files, [FromHeader(Name = "Authentication")] string token)
    {
        var isValid = _manager.ValidateAccessToken(token);
         if(isValid.statusCode == 200){
            //Get the product 
            string sub = _manager.ExtractSub(token)!;
            Prodotto? prod = _context.prodotto.Where(prod => prod.autore == sub).OrderBy(prod => prod.created_at).Last();                
            if(prod != null){
                //Get the uploaded images names (only 5 images per post are allowed)
                int numberOfNewFiles = files.Count;
                int rangeToSelect = numberOfNewFiles > 5 ? 4 : numberOfNewFiles;
                var uploadedFileName = files.Select(file => file.FileName).ToList().GetRange(0, rangeToSelect);
                foreach (var file in files.GetRange(0, rangeToSelect))
                {
                    var filename = _manager.encode(HmacHash(Guid.NewGuid()))+".jpeg";
                    var imgPath = $"products/{sub}/{prod.id}/{filename}";
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
                        var index = uploadedFileName.IndexOf(file.FileName);
                        uploadedFileName[index] = filename;
                    }catch(AmazonS3Exception ex){
                        Console.WriteLine($"Error: '{ex.Message}' when writing an object");  
                    }
                }
                await UploadImageNameToDb(uploadedFileName, sub, prod.id);
                //removeOldFiles(uploadedFiles, sub, prod.id);
                return Ok();
            }else{
                return BadRequest("Prodotto non valido");
            }
        }
        return StatusCode(isValid.statusCode);
    }

    
    private async Task UploadImageNameToDb(List<string> immaginiProdotto, string sub, Guid? prodId){
        const string updateQuery = 
        """
            UPDATE prodotto SET immagini_prodotto = @lista_immagini WHERE id = @prod_id;
        """;
        await using var command = conn.CreateCommand(updateQuery);
        command.Parameters.Add("@prod_id", NpgsqlDbType.Uuid).Value = prodId;
        command.Parameters.Add("@lista_immagini", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = immaginiProdotto;

        await command.ExecuteNonQueryAsync();
    }

    //extract the filename from the s3 path
    private static string ExtractImageNameFromPath(string imgPath){
        string[] imgPathParts = imgPath.Split('/');
        return imgPathParts.Last();
    }

    private static List<string> GetImagesToDelete(List<string> oldImages, List<string> newImages){
        //if the newImages list is empty it means that the user wants to delete all the product images
        if(newImages.Count == 0){
            return oldImages;
        }else{
            return oldImages.Except(newImages).ToList();
        }
    }

    private List<string> GetImagesToUpload(List<string> imagesToDelete, List<string> oldImages, List<string> newImages){
        //filter all the images that have been already uploaded from the images that haven't yet
        if(newImages.Count == 0){
            return newImages;
        }else{
            
            List<string> fileNames = oldImages.Except(imagesToDelete).ToList();
            for(int i = 0; i < newImages.Count; i++){
                if(!oldImages.Contains(newImages[i])){
                    newImages[i] = $"{_manager.encode(HmacHash(Guid.NewGuid()))}";
                }
            }
            return newImages;
        }      
    }

    private async Task DeleteProductImagesFromBucket(string sub, string prodId, List<string> imagesToDelete){
        for(int i = 0; i < imagesToDelete.Count; i++){
            var imgPath = $"products/{sub}/{prodId}/{imagesToDelete[i]}";
            DeleteObjectRequest request = new()
            {
                BucketName = "fuocherello-bucket",
                Key = imgPath,
            };
            try{
                await s3Client.DeleteObjectAsync(request);  
            }catch(AmazonS3Exception ex){
                Console.WriteLine($"Error: '{ex.Message}' when writing an object");  
            }
        } 
    } 

    private async Task PostProductImagesToBucket(string sub, string prodId, List<string> fileNamesToUpload, List<IFormFile> files){
        for(int i = 0; i < fileNamesToUpload.Count; i++){
            if(!fileNamesToUpload.Contains(files[i].FileName)){
                var imgPath = $"products/{sub}/{prodId}/{fileNamesToUpload[i]}";
                PutObjectRequest request = new()
                {
                    BucketName = "fuocherello-bucket",
                    Key = imgPath,
                    InputStream = files[i].OpenReadStream(),
                    ContentType = files[i].ContentType,
                    UseChunkEncoding = false,
                    CannedACL = S3CannedACL.PublicRead
                };
                try{     
                    //delete selcted image file from the cloud
                    await s3Client.PutObjectAsync(request);    
                }catch(AmazonS3Exception ex){
                    Console.WriteLine($"Error: '{ex.Message}' when writing an object");  
                }
            }
        }
        await UploadImageNameToDb(fileNamesToUpload, sub, Guid.Parse(prodId)); 
    } 
    
    [HttpPut("Prodotto/{id}")]
    //edit product images after the product has been published
    public async Task<ActionResult<Prodotto>> PutProdottoImagesToBucket([FromForm] List<IFormFile> files, [FromHeader(Name = "Authentication")] string token, Guid id)
    {
        try
        {  
            Console.WriteLine("MODIFICANDO IMMAGINI DEL POST");
           
            MyStatusCodeResult isValid = _manager.ValidateAccessToken(token);
            if(isValid.statusCode == 200){
                //check if product exists
                string sub = _manager.ExtractSub(token)!;
                Prodotto?prod =_context.prodotto.FirstOrDefault(prod => prod.autore == sub && prod.id == id);    
                if(prod != null){
                    //Get old images filenames from bucket
                    ListObjectsV2Request listOldFilesRequest = new()
                    {                            
                        BucketName = "fuocherello-bucket",
                        Prefix = $"products/{sub}/{prod!.id}",
                        StartAfter = $"products/{sub}/{prod.id}"
                    }; 
                    
                    ListObjectsV2Response oldFiles = await s3Client.ListObjectsV2Async(listOldFilesRequest);
                    
                    int numberOfOldFiles = oldFiles.S3Objects.Count;
                    int numberOfNewFiles = files.Count;
                    Console.WriteLine($"CI SONO {numberOfNewFiles} IMMAGINI NUOVE");
                    Console.WriteLine($"CI SONO {numberOfOldFiles} IMMAGINI VECCHIE");
                    List<string> oldFileName = new();
                    
                    if(numberOfOldFiles > 0){
                        oldFileName = oldFiles.S3Objects.Select(file => ExtractImageNameFromPath(file.Key)).ToList();
                    }else if(numberOfOldFiles == 0){
                        List<string> newFileNames = new();
                        for(int i = 0; i < numberOfNewFiles && i < 5; i++){    
                            newFileNames.Add($"{_manager.encode(HmacHash(Guid.NewGuid()))}.jpeg");
                        }
                        await PostProductImagesToBucket(sub, prod.id.ToString()!, newFileNames, files);
                        return Ok();
                    }

                    List<string> newFileName = new();
          
                    if(numberOfNewFiles > 0){
                        Console.WriteLine("SONO STATE MANDATE NUOVE IMMAGINI");
                        newFileName = files.GetRange(0, numberOfNewFiles >= 5 ? 4 : numberOfNewFiles).Select(file => file.FileName).ToList();
                        numberOfNewFiles = newFileName.Count;
                    }else if(numberOfNewFiles == 0){
                       
                        await DeleteProductImagesFromBucket(sub, prod.id.ToString()!, oldFileName);
                        await UploadImageNameToDb(newFileName, sub, prod.id);
                        return Ok();
                    }

                    List<string> imagesToDelete = GetImagesToDelete(oldFileName, newFileName);
                    await DeleteProductImagesFromBucket(sub, prod.id.ToString()!, imagesToDelete);
                
                    List<string> fileNamesToUpload = GetImagesToUpload(imagesToDelete, oldFileName, newFileName);
                    await PostProductImagesToBucket(sub, prod.id.ToString()!, fileNamesToUpload, files);                  
                    return Ok();

                }else{ 
                    Console.WriteLine("PRODOTTO NON ESISTENTE");
                    return BadRequest("PRODOTTO NON ESISTENTE");
                }
            }
            
            return StatusCode(isValid.statusCode);
        }
        catch (Exception e)
        {  
            Console.WriteLine(e);
            return BadRequest(e);
        }
    }

    [HttpDelete("Prodotto/{id}")]
    public async Task<IActionResult> DeletePordottoImagesFromBcuket([FromHeader(Name = "Authentication")] string token, Guid id){
        try
        {       
            MyStatusCodeResult isValid = _manager.ValidateAccessToken(token);
            if(isValid.statusCode == 200){
                //check if product exists
                string sub = _manager.ExtractSub(token)!;
                Prodotto?prod =_context.prodotto.FirstOrDefault(prod => prod.autore == sub && prod.id == id);    
                if(prod != null){
                    //Get old images filenames from bucket
                    ListObjectsV2Request listOldFilesRequest = new()
                    {                            
                        BucketName = "fuocherello-bucket",
                        Prefix = $"products/{sub}/{prod!.id}",
                        StartAfter = $"products/{sub}/{prod.id}"
                    }; 
                    
                    ListObjectsV2Response oldFiles = await s3Client.ListObjectsV2Async(listOldFilesRequest);
                    int numberOfOldFiles = oldFiles.S3Objects.Count;
                    List<string> oldFileName = new();
                    
                    if(numberOfOldFiles > 0){
                        oldFileName = oldFiles.S3Objects.Select(file => ExtractImageNameFromPath(file.Key)).ToList();
                        await DeleteProductImagesFromBucket(sub, prod.id.ToString()!, oldFileName);
                    }else if(numberOfOldFiles == 0){
                        return Ok();
                    }
                }else{ 
                    Console.WriteLine("PRODOTTO NON ESISTENTE");
                    return BadRequest("PRODOTTO NON ESISTENTE");
                }
            }
            return StatusCode(isValid.statusCode);
        }
        catch (Exception e)
        {  
            Console.WriteLine(e);
            return BadRequest(e);
        }
    }


    [HttpPut("Propic")]
    //edit product images after the product has been published
    public async Task<ActionResult<Prodotto>> PutUserProfilePicToBucket(IFormFile uploadedFile, [FromHeader(Name = "Authentication")] string token)
    {
        try
        {  
            MyStatusCodeResult isValid = _manager.ValidateAccessToken(token);
            if(isValid.statusCode == 200){
                //check if product exists
                string sub = _manager.ExtractSub(token)!;
                    
                if(sub != null){
                    //Get old images filenames from bucket
                    ListObjectsV2Request listOldFilesRequest = new()
                    {                            
                        BucketName = "fuocherello-bucket",
                        Prefix = $"profiles/{sub}",
                        StartAfter = $"profiles/{sub}"
                    }; 

                    //check if a user's propic already exits, if so copy the image name
                    ListObjectsV2Response oldPropic = await s3Client.ListObjectsV2Async(listOldFilesRequest);
                    if(uploadedFile.ContentType.Contains("image")){
                        if(oldPropic.S3Objects.Count == 0){
                            string propicName = sub + ".jpeg";       
                            PutObjectRequest request = new()
                            {
                                BucketName = "fuocherello-bucket",
                                Key = $"profiles/{sub}/{propicName}",
                                InputStream = uploadedFile.OpenReadStream(),
                                ContentType = uploadedFile.ContentType,
                                UseChunkEncoding = false,
                                CannedACL = S3CannedACL.PublicRead
                            };
                            await s3Client.PutObjectAsync(request);
                        }else{
                            string propicName = ExtractImageNameFromPath(oldPropic.S3Objects[0].Key);
                            PutObjectRequest request = new()
                            {
                                BucketName = "fuocherello-bucket",
                                Key = $"profiles/{sub}/{propicName}",
                                InputStream = uploadedFile.OpenReadStream(),
                                ContentType = uploadedFile.ContentType,
                                UseChunkEncoding = false,
                                CannedACL = S3CannedACL.PublicRead
                            };
                            await s3Client.PutObjectAsync(request);
                        }
                    }else{
                        return BadRequest("File non valido");
                    }  
                    return Ok();
                }else{ 
                    return NotFound("Prodotto non trovato");
                }
            }
            return StatusCode(isValid.statusCode);
        }
        catch (DirectoryNotFoundException)
        {  
            Console.WriteLine("DIRECTORY NOT FOUND");
            return BadRequest("DIRECTORY NOT FOUND");
        }
    }

    [HttpDelete("Propic")]
    //edit product images after the product has been published
    public async Task<IActionResult> DeleteUserProfilePicFromBucket(IFormFile uploadedFile, [FromHeader(Name = "Authentication")] string token)
    {
        MyStatusCodeResult isValid = _manager.ValidateAccessToken(token);
        if(isValid.statusCode == 200){
            //check if product exists
            string sub = _manager.ExtractSub(token)!;
            if(sub != null){
                
                DeleteObjectRequest request = new()
                {
                    BucketName = "fuocherello-bucket",
                    Key = $"products/{sub}/",
                };
                await s3Client.DeleteObjectAsync(request);
                return Ok();
            }else{ 
                return NotFound("Utente non trovato");
            }
        }
        return StatusCode(isValid.statusCode); 
    }

    
    [HttpPut("Chat")]
    public async Task<IActionResult> PutChatPicToBucket([FromForm(Name = "files")] List<IFormFile> files, [FromForm] string chatId, [FromHeader(Name = "Authentication")] string token){
        MyStatusCodeResult isValid = _manager.ValidateAccessToken(token);
        if(isValid.statusCode == 200){
            //check if the chat exists and if the user partecipate in the chat
            string? sub = _manager.ExtractSub(token);
            ChatList? chat = _context.lista_chat.SingleOrDefault(chat => chat.id == Guid.Parse(chatId) && (chat.compratore_id == sub || chat.venditore_id == sub));
            
            if(sub != null && chat != null){
                List<string> imgurls = new();
                List<UserMessage> messages = new();
                foreach(var file in files){
                    if(file.ContentType.Contains("image")){
                        string imageName = _manager.encode(HmacHash(Guid.NewGuid())) + ".jpeg";       
                        PutObjectRequest request = new()
                        {
                            BucketName = "fuocherello-bucket",
                            Key = $"chats/{chatId}/{imageName}",
                            InputStream = file.OpenReadStream(),
                            ContentType = file.ContentType,
                            UseChunkEncoding = false,
                            CannedACL = S3CannedACL.PublicRead
                        };
                        imgurls.Add($"https://fuocherello-bucket.s3.cubbit.eu/chats/{chatId}/{imageName}");
                        UserMessage message = new();
                        message.id = Guid.NewGuid();
                        message.chat_id = Guid.Parse(chatId);
                        message.mandante_id = sub;
                        message.prod_id = chat.prod_id;
                        message.messaggio = $"https://fuocherello-bucket.s3.cubbit.eu/chats/{chatId}/{imageName}";
                        message.delivered = false;
                        message.sent_at = ((ulong)((DateTimeOffset) DateTime.Now).ToUnixTimeMilliseconds());
                        messages.Add(message);
                        await _context.messaggio.AddAsync(message);
                        await _context.SaveChangesAsync();
                        //send the image to the s3 storage
                        await s3Client.PutObjectAsync(request);
                    }else{
                        return BadRequest();
                    }                 
                }
                await signalRClient.StartAsync();
                var receiver = sub == chat.compratore_id ? chat.venditore_id : chat.compratore_id;
                await signalRClient.InvokeAsync("SendImageToClient", _configuration.GetValue<string>("SignalRServerPassword")!, receiver!, JsonSerializer.Serialize(messages));
                
                return Ok(JsonSerializer.Serialize(messages));
            }else{ 
                return NotFound("Dati forniti non validi");
            }
        }
        return StatusCode(isValid.statusCode);
    }
}