using Microsoft.AspNetCore.Mvc;
using final.Data;
using final.Models;
using System.Security.Cryptography;
using NpgsqlTypes;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using Newtonsoft.Json.Linq;
using Amazon.S3.Model;
using Amazon.S3;
using Amazon.Runtime;
using System.Text;
using Npgsql;
using NuGet.Protocol;

namespace final.Controllers;

[ApiController]
[Route("api/[controller]")]

public class ProdottoController : ControllerBase
{
    private readonly ApiServerContext _context;
    private readonly IAmazonS3 _s3Client;
    private readonly JwtManager _manager;
    private readonly IConfiguration _configuration;
    private readonly NpgsqlDataSource _conn;
    public ProdottoController(ApiServerContext context, RSA key, IConfiguration configuration,  NpgsqlDataSource conn)
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
        _s3Client = new AmazonS3Client(creds, config);
    }

    static bool CheckTitle(string? element){
        if(element != null){
            if(element != "" && element.Length > 0 && element.Length < 100){
                return true;
            }
        }
        return false;
    }

    static bool CheckDescription(string? element){
        if(element != null){
            if(element != "" && element.Length > 0 && element.Length < 300){  
                return true;
            }
        }
        return false;
    }

    
    private static string? GetProductType(ProdottoDTO p){  
        if (p.categoria != "legname" && p.categoria != "biomasse" && p.categoria != "pellet"){
            return null;
        }else{
            return p.categoria;
        }
    }

    static bool IsPriceValid(double price) => price >= 0;
    
    // GET: api/Prodotto
    [HttpGet]
    public ActionResult GetProdotto()
    {
        try
        {
            return Ok(ToJson(_context.prodotto.ToList()));
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        return BadRequest();

    }
    private static string ToJson(List<Prodotto> prodotti){
        
        List<Dictionary<string, object>> formattedData = new();
        for(int i = 0; i < prodotti.Count; i++)
        {
            Dictionary<string, object> data = new()
            {
                { "id", prodotti[i].id! },
                { "autore", prodotti[i].autore! },
                { "titolo", prodotti[i].titolo! },
                { "descrizione", prodotti[i].descrizione! },
                { "prezzo", prodotti[i].prezzo },
                { "immagini_prodotto", prodotti[i].immagini_prodotto! },
                { "categoria", prodotti[i].categoria! },
                { "created_at", prodotti[i].created_at! },
                { "luogo_di_pubblicazione", prodotti[i].luogo_di_pubblicazione! },
                { "tipo_autore", "utente" }
            };
            formattedData.Add(data);
        }
        var json = JsonSerializer.Serialize(formattedData);
        return json;
    } 

    [HttpGet("title")]
    public async Task<ActionResult<Prodotto>> GetProdottoByTitle([FromQuery] string q)
    {
        const string fullTextQuery = 
        """
            SELECT id, autore, titolo, descrizione, prezzo, immagini_prodotto, categoria, created_at, luogo_di_pubblicazione, tipo_autore 
            FROM prodotto
            WHERE ts @@ to_tsquery('italian', @input);
        """;

        const string singleWordQuery = 
        """
            SELECT id, autore, titolo, descrizione, prezzo, immagini_prodotto, categoria, created_at, luogo_di_pubblicazione, tipo_autore
            FROM prodotto
            WHERE LOWER(titolo) LIKE LOWER(@input);
        """;

        string query = q.Split('+').Length > 1 ? fullTextQuery : singleWordQuery;
        await using var command = _conn.CreateCommand(query);
        command.Parameters.Add("@input", NpgsqlDbType.Text).Value = "%" + q + "%";
        await using var reader = await command.ExecuteReaderAsync();
        List<Dictionary<string, object>> formattedData = new();

        while (await reader.ReadAsync())
        {
            var catValue = reader[6].ToString();
            if(catValue != "legname" && catValue != "biomasse" && catValue != "pellet"){
                return BadRequest("categoria errata");
            }

            Dictionary<string, object> data = new()
            {
                { "id", reader.GetGuid(0) },
                { "autore", reader.GetString(1) },
                { "titolo", reader.GetString(2) },
                { "descrizione", reader.GetString(3) },
                { "prezzo", reader.GetDouble(4) },
                { "immagini_prodotto", reader[5] },
                { "categoria", catValue },
                { "created_at", reader.GetDateTime(7) },
                { "luogo_di_pubblicazione", reader.GetString(8) },
                { "tipo_autore", "utente" }
            };
            formattedData.Add(data);
        }
        var json = JsonSerializer.Serialize(formattedData);
        Console.WriteLine(json);
        return Ok(json);
    }

    [HttpGet("tipo")]
    // GET: api/Prodotto/tipo]
    public ActionResult GetProdottoByTipo(string tipo)
    {
        try
        {
            if(tipo != "legname" && tipo != "biomasse" && tipo != "pellet"){
                return BadRequest("Tipo Prodotto errato");
            }else{
                //List<Prodotto> prodotti_filtrati = 
                Console.WriteLine("prodotti selezionati");
                if(tipo == "legname"){
                    return Ok(JsonSerializer.Serialize(_context.Legname.ToList()));
                }else if(tipo == "biomasse"){
                    return Ok(JsonSerializer.Serialize(_context.Biomasse.ToList()));
                }
                return Ok(JsonSerializer.Serialize(_context.Pellet.ToList()));
                    
            }
        }
        catch (Exception e)
        {
            return BadRequest(e.Message);
        }

    }

    [HttpGet("autore")]
    // GET: api/Prodotto?id=userid]
    public ActionResult<Prodotto> GetProdottoByUtente(string id)
    {
        List<Prodotto> prodotti_pubblicati =_context.prodotto.Where(prod => prod.autore == id).ToList();
        if(prodotti_pubblicati.Count > 0){
            return Ok(ToJson(prodotti_pubblicati));
        }
        return Ok();
    }

    [HttpGet("preferiti")]
    public ActionResult GetPreferitiUtente ([FromHeader(Name = "Authentication")] string token) 
    {  

        MyStatusCodeResult isValid = _manager.ValidateAccessToken(token);
        if (isValid.statusCode == 200){
            string role = _manager.ExtractRole(token) ?? ""; 
            string sub = _manager.ExtractSub(token) ?? "";
            
            if (role != "utente" && role != "azienda"){
                return Forbid();
            }else{  
                List<Preferito> preferiti = _context.preferito.Where(pref => pref.user_id == sub).ToList();
                List<Prodotto> prodotti = new();
                if(preferiti.Count > 0){
                    foreach(var preferito in preferiti){
                        prodotti.Add(_context.prodotto.FirstOrDefault(prod => prod.id == preferito.prod_id)!);
                    }
                    return Ok(ToJson(prodotti));
                }else{
                    return NoContent();
                }
            }
        }
        return StatusCode(isValid.statusCode);
    }

    // GET: api/prodotto/5
    [HttpGet("{id}")]
    public ActionResult<Prodotto> GetProdottoById(Guid id)
    {
        Prodotto? searched_prod = _context.prodotto.FirstOrDefault(prod => prod.id == id);
        if(searched_prod == null){
            return NotFound();
        }else{
            List<Prodotto> p = new()
            {
                searched_prod
            };
            return Ok(ToJson(p));
        }
    }

    // PUT: api/prodotto/5
    // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
    [HttpPut("{id}")]
    public async Task<ActionResult> PutProdotto([FromHeader(Name = "Authentication")] string token, [FromForm] Prodotto Prodotto, [FromForm(Name = "files")] List<IFormFile> files)
    {
        try{
            MyStatusCodeResult isValid = _manager.ValidateAccessToken(token);

            if (isValid.statusCode == 200)
            {
                string? role = _manager.ExtractRole(token);
                string? sub = _manager.ExtractSub(token);
                Console.WriteLine(sub);
                Prodotto? productToUpdate = _context.prodotto.FirstOrDefault(prod => prod.autore == sub && prod.id == Prodotto.id);
                
                if(productToUpdate == null){
                    return BadRequest();
                }

                if (role != "utente" && role != "azienda")
                {
                    return Forbid();
                }

                if(files.Count > 0){
                    productToUpdate.immagini_prodotto = await PutProdottoImagesToBucket(files, productToUpdate);
                    if(productToUpdate.immagini_prodotto.Count == 0){
                        Console.WriteLine("Errore nell'inserimento delle immagini");
                        return BadRequest();
                    }
                }    
                
                if(CheckTitle(Prodotto.titolo) && CheckDescription(Prodotto.descrizione) && IsPriceValid(Prodotto.prezzo) && Prodotto.categoria != null)
                {
                    if(productToUpdate != null){   
                        bool isTitleModified = productToUpdate.titolo != Prodotto.titolo;
                        bool isDescriptionModified = productToUpdate.descrizione != Prodotto.descrizione;
                        bool isPriceModified  = productToUpdate.prezzo != Prodotto.prezzo;
                        bool isCategoryModified = productToUpdate.categoria != Prodotto.categoria;
                        
                        if(isTitleModified || isDescriptionModified || isPriceModified || isCategoryModified){
                            _context.Attach(productToUpdate);
                            
                            if(isTitleModified){
                                productToUpdate.titolo = Prodotto.titolo;
                            }
                            if(isCategoryModified){
                                productToUpdate.descrizione = Prodotto.descrizione;
                            }
                            if(isPriceModified){
                                productToUpdate.prezzo = Prodotto.prezzo;
                            }
                            if(isCategoryModified){
                                productToUpdate.categoria = Prodotto.categoria;
                            }
                            productToUpdate.ultima_modifica = DateTime.Now;

                            _context.Entry(productToUpdate).Property(p => p.titolo).IsModified = isTitleModified;
                            _context.Entry(productToUpdate).Property(p => p.descrizione).IsModified = isDescriptionModified;
                            _context.Entry(productToUpdate).Property(p => p.prezzo).IsModified = isPriceModified;
                            _context.Entry(productToUpdate).Property(p => p.categoria).IsModified = isCategoryModified;
                            _context.Entry(productToUpdate).Property(p => p.ultima_modifica).IsModified = true;
                            await _context.SaveChangesAsync();
                            return Ok(JsonSerializer.Serialize(productToUpdate));
                        }else{
                            return StatusCode(304);
                        }
                    }
                }
            }
            return StatusCode(isValid.statusCode);
        }catch(Exception e){
            return BadRequest(e.Message);
        }
        
    }
    [HttpPost("preferiti")]
    public async Task<ActionResult<Prodotto>> NuovoPreferitoAsync([FromHeader(Name = "Authentication")] string token, [FromHeader(Name = "ProdID")] Guid prod_id)
    {
        MyStatusCodeResult isValid = _manager.ValidateAccessToken(token);
        if (isValid.statusCode == 200)
        {
            JwtSecurityTokenHandler jwtHandler = new();
            var jwt = jwtHandler.ReadJwtToken(token);
            var payloadString = jwt.Payload.SerializeToJson();
            JObject payload = JObject.Parse(payloadString);
            string role = payload["role"]!.ToString();
            string sub = payload["sub"]!.ToString();
            if (role != "utente" && role != "azienda")
            {
                return Forbid();
            }
            else
            {
                var selected_prod = _context.prodotto.FirstOrDefault(prod => prod.id == prod_id);
                if(selected_prod != null){
                    Preferito NuovoPreferito = new();
                    NuovoPreferito.id = Guid.NewGuid();
                    NuovoPreferito.prod_id = prod_id;
                    NuovoPreferito.user_id = sub;
                    _context.preferito.Add(NuovoPreferito);
                    await _context.SaveChangesAsync();
                    return Ok();
                }else{
                    return NotFound();
                }     
            }
        }
        return StatusCode(isValid.statusCode);
    }

    [HttpPost]
    public async Task<ActionResult<Prodotto>> PostProdottoAsync([FromHeader(Name = "Authentication")] string token, [FromForm] ProdottoDTO Prodotto, [FromForm(Name = "files")] List<IFormFile> files)
    {
        try{
            MyStatusCodeResult isValid = _manager.ValidateAccessToken(token);
            if (isValid.statusCode == 200)
            {
                string? role = _manager.ExtractRole(token);
                string? sub = _manager.ExtractSub(token);         

                if (role != "utente" && role != "azienda")
                {
                    return Forbid();
                }
                else if(CheckTitle(Prodotto.titolo) && CheckDescription(Prodotto.descrizione) && IsPriceValid(Prodotto.prezzo) && GetProductType(Prodotto) != null)
                {
                    //set prodct's information
                    Console.Write("creando il prodotto");
                    Prodotto NuovoProdotto = new()
                    {
                        id = Guid.NewGuid(),
                        titolo = Prodotto.titolo,
                        autore = sub,
                        luogo_di_pubblicazione = Prodotto.luogo_di_pubblicazione,
                        descrizione = Prodotto.descrizione,
                        prezzo = Prodotto.prezzo < 0 ? 0 : Prodotto.prezzo,
                        categoria = Prodotto.categoria
                    };

                    //upload images
                    if (files.Count > 0){
                        NuovoProdotto.immagini_prodotto = await PostProdottoImagesToBucket(NuovoProdotto, files);
                        if(NuovoProdotto.immagini_prodotto.Count == 0){
                            Console.WriteLine("Errore nell'inserimento delle immagini");
                            return BadRequest();
                        }
                    }        
                    _context.prodotto.Add(NuovoProdotto);
                    await _context.SaveChangesAsync();
                    return Ok(JsonSerializer.Serialize(NuovoProdotto));
                }
            }
            return StatusCode(isValid.statusCode);
        }catch(Exception e){
            return BadRequest(e.Message);
        }
    }

    // DELETE: api/prodotto/5
    [HttpDelete("preferiti/{id}")]
    public async Task<IActionResult> DeletePreferitoAsync(Guid id, [FromHeader(Name = "Authentication")] string token)
    {
        try{
            MyStatusCodeResult isValid = _manager.ValidateAccessToken(token);
            if (isValid.statusCode == 200)
            {
                string? role = _manager.ExtractRole(token); 
                string? sub = _manager.ExtractSub(token);
                
                if ( role != "utente" && role != "azienda")
                {
                    return Forbid();
                }
                else
                {
                    Preferito? preferitoDaEliminare = _context.preferito.FirstOrDefault(pref => pref.prod_id == id && pref.user_id == sub);
                    if(preferitoDaEliminare != null){
                        _context.preferito.Remove(preferitoDaEliminare);
                        await _context.SaveChangesAsync();
                    }
                    return Ok();
                }
            }
            return StatusCode(isValid.statusCode);
        }catch(Exception e){
            return BadRequest(e.Message);
        }
    }


    // DELETE: api/prodotto/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProdottoAsync(Guid id, [FromHeader(Name = "Authentication")] string token)
    {
        try{
            MyStatusCodeResult isValid = _manager.ValidateAccessToken(token);
            if (isValid.statusCode == 200)
            {
                string? sub = _manager.ExtractSub(token); 
                string? role = _manager.ExtractRole(token);
                
                if ( role != "utente" && role != "azienda")
                {
                    return Forbid();
                }
                else
                {
                    var prod = await _context.prodotto.FindAsync(id);
                    if(prod != null && prod.autore == sub){
                        var folderPath = $"products/{sub}/{id}";

        
                        try{     
                            //delete selcted image file from the cloud
                            var listObjectsRequest = new ListObjectsRequest
                            {
                                BucketName = "fuocherello-bucket",
                                Prefix = folderPath
                            };

                            var listObjectsResponse = await _s3Client.ListObjectsAsync(listObjectsRequest);

                            var deleteObjectsRequest = new DeleteObjectsRequest
                            {
                                BucketName = "fuocherello-bucket",
                                Objects = listObjectsResponse.S3Objects.Select(obj => new KeyVersion { Key = obj.Key }).ToList()
                            };

                            var deleteObjectsResponse = await _s3Client.DeleteObjectsAsync(deleteObjectsRequest);
                            Console.WriteLine($"Successfully deleted {deleteObjectsResponse.DeletedObjects.Count} objects.");
                            
                            Console.WriteLine(deleteObjectsResponse.ToJson());
                            _context.prodotto.Remove(prod);
                            await _context.SaveChangesAsync();
                        }catch(AmazonS3Exception ex){
                            Console.WriteLine($"Error: '{ex.Message}' when deleting an object");  
                        }

                    }
                }
            }

            return StatusCode(isValid.statusCode);
        }catch(Exception e){
            return BadRequest(e.Message);
        } 
    }

    private bool ProdottoExists(Guid id) => (_context.prodotto?.Any(e => e.id == id)).GetValueOrDefault();
    
    //IMAGES MANAGEMENT
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
                string url_safe_id = _manager!.encode(hash);

                Console.WriteLine("HMAC hash: " + hash);
                return url_safe_id;
            }
        }
        return "";
    }
    private async Task UploadImageNameToDb(List<string> immaginiProdotto, string sub, Guid? prodId){
        const string updateQuery = 
        """
            UPDATE Prodotto SET immagini_Prodotto = @lista_immagini WHERE id = @prod_id;
        """;
        await using var command = _conn.CreateCommand(updateQuery);
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
    private async Task UploadProductImagesToBucket(string sub, string prodId, List<string> fileNamesToUpload, List<IFormFile> files){
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
                await _s3Client.PutObjectAsync(request);    
            }catch(AmazonS3Exception ex){
                Console.WriteLine($"Error: '{ex.Message}' when writing an object");  
            }
        }
    }
    await UploadImageNameToDb(fileNamesToUpload, sub, Guid.Parse(prodId)); 
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
                await _s3Client.DeleteObjectAsync(request);  
            }catch(AmazonS3Exception ex){
                Console.WriteLine($"Error: '{ex.Message}' when writing an object");  
            }
        } 
    } 

    private async Task<List<string>> PostProdottoImagesToBucket(Prodotto prod, List<IFormFile> files){
        //Get the uploaded images names (only 5 images per post are allowed)
        string sub = prod.autore!;
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
                PutObjectResponse response = await _s3Client.PutObjectAsync(request);  
                var index = uploadedFileName.IndexOf(file.FileName);
                uploadedFileName[index] = filename;
            }catch(AmazonS3Exception ex){
                Console.WriteLine($"Error: '{ex.Message}' when writing an object");
                return new List<string>();
            }
        }
        await UploadImageNameToDb(uploadedFileName, sub, prod.id);
        //removeOldFiles(uploadedFiles, sub, prod.id);
        return uploadedFileName;
    }

    private async Task<List<string>> PutProdottoImagesToBucket(List<IFormFile> files, Prodotto prod)
    {
        try
        {  
            //check if product exists
            if(prod != null){
                //Get old images filenames from bucket
                ListObjectsV2Request listOldFilesRequest = new()
                {                            
                    BucketName = "fuocherello-bucket",
                    Prefix = $"products/{prod.autore}/{prod!.id}",
                    StartAfter = $"products/{prod.autore}/{prod.id}"
                }; 
                
                ListObjectsV2Response oldFiles = await _s3Client.ListObjectsV2Async(listOldFilesRequest);
                
                int numberOfOldFiles = oldFiles.S3Objects.Count;
                int numberOfNewFiles = files.Count;

                List<string> oldFileName = new();
                List<string> newFileName = new();

                if(numberOfOldFiles > 0){
                    oldFileName = oldFiles.S3Objects.Select(file => ExtractImageNameFromPath(file.Key)).ToList();
                }else if(numberOfOldFiles == 0){
                
                    for(int i = 0; i < numberOfNewFiles && i < 5; i++){    
                        newFileName.Add($"{_manager.encode(HmacHash(Guid.NewGuid()))}.jpeg");
                    }
                    await UploadProductImagesToBucket(prod.autore!, prod.id.ToString()!, newFileName, files);
                    return newFileName;
                }

                if(numberOfNewFiles > 0){
                    newFileName = files.GetRange(0, numberOfNewFiles >= 5 ? 4 : numberOfNewFiles).Select(file => file.FileName).ToList();
                    numberOfNewFiles = newFileName.Count;
                }else if(numberOfNewFiles == 0){
                    
                    await DeleteProductImagesFromBucket(prod.autore!, prod.id.ToString()!, oldFileName);
                    await UploadImageNameToDb(newFileName, prod.autore!, prod.id);
                    return newFileName;
                }

                List<string> imagesToDelete = GetImagesToDelete(oldFileName, newFileName);
                await DeleteProductImagesFromBucket(prod.autore!, prod.id.ToString()!, imagesToDelete);
            
                List<string> fileNamesToUpload = GetImagesToUpload(imagesToDelete, oldFileName, newFileName);
                await UploadProductImagesToBucket(prod.autore!, prod.id.ToString()!, fileNamesToUpload, files);                  
                return fileNamesToUpload;

            }else{ 
                return new List<string>();
            }
        }
        catch (Exception e)
        {  
            Console.WriteLine(e);
            return new List<string>();
        }
    }

}

