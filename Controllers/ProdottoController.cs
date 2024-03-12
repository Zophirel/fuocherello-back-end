using Microsoft.AspNetCore.Mvc;
using Fuocherello.Data;
using Fuocherello.Models;
using NpgsqlTypes;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using Newtonsoft.Json.Linq;
using Amazon.S3.Model;
using Amazon.S3;
using Amazon.Runtime;
using System.Text;
using Npgsql;
using System.Security.Cryptography;
using Fuocherello.Singleton.JwtManager;
namespace Fuocherello.Controllers;

[ApiController]
[Route("api/[controller]")]

public class ProductController : ControllerBase
{
    private readonly ApiServerContext _context;
    private readonly IAmazonS3 _s3Client;
    private readonly IJwtManager _manager;
    private readonly IConfiguration _configuration;
    private readonly NpgsqlDataSource _conn;
    public ProductController(ApiServerContext context,  IConfiguration configuration,  NpgsqlDataSource conn, IJwtManager manager)
    {
        _conn = conn;
        _context = context;
        _manager = manager;
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

    private static bool CheckTitle(string? element){
        if(element != null){
            if(element != "" && element.Length > 0 && element.Length < 100){
                return true;
            }
        }
        return false;
    }

    private static bool CheckDescription(string? element){
        if(element != null){
            if(element != "" && element.Length > 0 && element.Length < 300){  
                return true;
            }
        }
        return false;
    }

    
    private static string? GetProductType(ProductDTO p){  
        if (p.Category != "legname" && p.Category != "biomasse" && p.Category != "pellet"){
            return null;
        }else{
            return p.Category;
        }
    }

    private static bool IsPriceValid(double price) => price >= 0;
    
    // GET: api/Product
    [HttpGet]
    public ActionResult GetProduct()
    {
        try
        {
            return Ok(ToJson(_context.Product.ToList()));
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        return BadRequest();

    }
    private static string ToJson(List<Product> products){
        
        List<Dictionary<string, object>> formattedData = new();
        for(int i = 0; i < products.Count; i++)
        {
            Dictionary<string, object> data = new()
            {
                { "id", products[i].Id! },
                { "author", products[i].Author! },
                { "title", products[i].Title! },
                { "description", products[i].Description! },
                { "price", products[i].Price },
                { "product_images", products[i].ProductImages! },
                { "category", products[i].Category! },
                { "created_at", products[i].CreatedAt! },
                { "place", products[i].Place! },
            };
            formattedData.Add(data);
        }
        var json = JsonSerializer.Serialize(formattedData);
        return json;
    } 

    [HttpGet("title")]
    public async Task<ActionResult<Product>> GetProductByTitle([FromQuery] string q)
    {
        const string fullTextQuery = 
        """
            SELECT id, author, title, description, price, product_images, category, created_at, place 
            FROM product
            WHERE ts @@ to_tsquery('italian', @input);
        """;

        const string singleWordQuery = 
        """
            SELECT id, author, title, description, price, product_images, category, created_at, place
            FROM product
            WHERE LOWER(title) LIKE LOWER(@input);
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
                { "author", reader.GetString(1) },
                { "title", reader.GetString(2) },
                { "description", reader.GetString(3) },
                { "price", reader.GetDouble(4) },
                { "product_images", reader[5] },
                { "category", catValue },
                { "created_at", reader.GetDateTime(7) },
                { "place", reader.GetString(8) },
            };
            formattedData.Add(data);
        }
        var json = JsonSerializer.Serialize(formattedData);
        Console.WriteLine(json);
        return Ok(json);
    }

    [HttpGet("category")]
    // GET: api/Product/category]
    public ActionResult GetProductByCategory(string category)
    {
        try
        {
            if(category != "legname" && category != "biomasse" && category != "pellet"){
                return BadRequest("Category Product errato");
            }else{
                //List<Product> products_filtrati = 
                Console.WriteLine("products selezionati");
                if(category == "legname"){
                    return Ok(JsonSerializer.Serialize(_context.Legname.ToList()));
                }else if(category == "biomasse"){
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

    [HttpGet("Author")]
    // GET: api/Product?id=userid]
    public ActionResult<Product> GetProductByUtente(string id)
    {
        List<Product> products_pubblicati =_context.Product.Where(prod => prod.Author == id).ToList();
        if(products_pubblicati.Count > 0){
            return Ok(ToJson(products_pubblicati));
        }
        return Ok();
    }

    [HttpGet("Saved")]
    public ActionResult GetPreferitiUtente ([FromHeader(Name = "Authentication")] string token) 
    {  

        MyStatusCodeResult isValid = _manager.ValidateAccessToken(token);
        if (isValid.StatusCode == 200){
            string role = _manager.ExtractRole(token) ?? ""; 
            string sub = _manager.ExtractSub(token) ?? "";
            
            if (role != "utente" && role != "azienda"){
                return Forbid();
            }else{  
                List<SavedProduct> saved_products = _context.SavedProduct.Where(pref => pref.UserId == sub).ToList();
                List<Product> products = new();
                if(saved_products.Count > 0){
                    foreach(var product in saved_products){
                        products.Add(_context.Product.FirstOrDefault(prod => prod.Id == product.ProdId)!);
                    }
                    return Ok(ToJson(products));
                }else{
                    return NoContent();
                }
            }
        }
        return StatusCode(isValid.StatusCode);
    }

    // GET: api/prodotto/5
    [HttpGet("{id}")]
    public ActionResult<Product> GetProductById(Guid id)
    {
        Product? searched_prod = _context.Product.FirstOrDefault(prod => prod.Id == id);
        if(searched_prod == null){
            return NotFound();
        }else{
            List<Product> p = new()
            {
                searched_prod
            };
            return Ok(ToJson(p));
        }
    }

    // PUT: api/prodotto/5
    // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
    [HttpPut("{id}")]
    public async Task<ActionResult> PutProduct([FromHeader(Name = "Authentication")] string token, [FromForm] Product Product, [FromForm(Name = "files")] List<IFormFile> files)
    {
        try{
            
            MyStatusCodeResult isValid = _manager.ValidateAccessToken(token);

            if (isValid.StatusCode == 200)
            {
                Console.WriteLine("PUT PRODUCT");
                string? role = _manager.ExtractRole(token);
                string? sub = _manager.ExtractSub(token);

                Console.WriteLine(sub);
                Product? productToUpdate = _context.Product.FirstOrDefault(prod => prod.Author == sub && prod.Id == Product.Id);
                
                if(productToUpdate == null){
                    return BadRequest();
                }

                if (role != "utente" && role != "azienda")
                {
                    return Forbid();
                }

         
                productToUpdate.ProductImages = await PutProductImagesToBucket(files, productToUpdate);
         
                if(CheckTitle(Product.Title) && CheckDescription(Product.Description) && IsPriceValid(Product.Price) && Product.Category != null)
                {
                    if(productToUpdate != null){   
                        bool isTitleModified = productToUpdate.Title != Product.Title;
                        bool isDescriptionModified = productToUpdate.Description != Product.Description;
                        bool isPriceModified  = productToUpdate.Price != Product.Price;
                        bool isCategoryModified = productToUpdate.Category != Product.Category;
                        
                        if(isTitleModified || isDescriptionModified || isPriceModified || isCategoryModified){
                            _context.Attach(productToUpdate);
                            
                            if(isTitleModified){
                                productToUpdate.Title = Product.Title;
                            }
                            if(isCategoryModified){
                                productToUpdate.Description = Product.Description;
                            }
                            if(isPriceModified){
                                productToUpdate.Price = Product.Price;
                            }
                            if(isCategoryModified){
                                productToUpdate.Category = Product.Category;
                            }
                            productToUpdate.UpdatedAt = DateTime.Now;

                            _context.Entry(productToUpdate).Property(p => p.Title).IsModified = isTitleModified;
                            _context.Entry(productToUpdate).Property(p => p.Description).IsModified = isDescriptionModified;
                            _context.Entry(productToUpdate).Property(p => p.Price).IsModified = isPriceModified;
                            _context.Entry(productToUpdate).Property(p => p.Category).IsModified = isCategoryModified;
                            _context.Entry(productToUpdate).Property(p => p.UpdatedAt).IsModified = true;
                            await _context.SaveChangesAsync();
                            return Ok(JsonSerializer.Serialize(productToUpdate));
                        }else{
                            return StatusCode(304);
                        }
                    }
                }
            }
            return StatusCode(isValid.StatusCode);
        }catch(Exception e){
            return BadRequest(e.Message);
        }
        
    }
    [HttpPost("saved")]
    public async Task<ActionResult<Product>> SaveProductAsync([FromHeader(Name = "Authentication")] string token, [FromHeader(Name = "ProdID")] Guid prodId)
    {
        MyStatusCodeResult isValid = _manager.ValidateAccessToken(token);
        if (isValid.StatusCode == 200)
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
                var selected_prod = _context.Product.FirstOrDefault(prod => prod.Id == prodId);
                if(selected_prod != null){
                    SavedProduct product = new()
                    {
                        Id = Guid.NewGuid(),
                        ProdId = prodId,
                        UserId = sub
                    };
                    _context.SavedProduct.Add(product);
                    await _context.SaveChangesAsync();
                    return Ok();
                }else{
                    return NotFound();
                }     
            }
        }
        return StatusCode(isValid.StatusCode);
    }

    [HttpPost]
    public async Task<ActionResult<Product>> PostProductAsync([FromHeader(Name = "Authentication")] string token, [FromForm] ProductDTO Product, [FromForm(Name = "files")] List<IFormFile> files)
    {
        try{

            MyStatusCodeResult isValid = _manager!.ValidateAccessToken(token);
   
            if (isValid.StatusCode == 200)
            {
                string? role = _manager.ExtractRole(token);
                string? sub = _manager.ExtractSub(token);         

                if (role != "utente" && role != "azienda")
                {
                    return Forbid();
                }
                else if(CheckTitle(Product.Title) && CheckDescription(Product.Description) && IsPriceValid(Product.Price) && GetProductType(Product) != null)
                {
                    //set prodct's information
                    Console.WriteLine("creando il prodotto");
                    Product NuovoProduct = new()
                    {
                        Id = Guid.NewGuid(),
                        Title = Product.Title,
                        Author = sub,
                        Place = Product.Place,
                        Description = Product.Description,
                        Price = Product.Price < 0 ? 0 : Product.Price,
                        Category = Product.Category
                    };

                    //upload images
                    if (files.Count > 0){
                        NuovoProduct.ProductImages = await PostProductImagesToBucket(NuovoProduct, files);
                        if(NuovoProduct.ProductImages.Count == 0){
                            Console.WriteLine("Errore nell'inserimento delle immagini");
                            return BadRequest();
                        }
                    }        
                    _context.Product.Add(NuovoProduct);
                    await _context.SaveChangesAsync();
                    return Ok(JsonSerializer.Serialize(NuovoProduct));
                }
            }
            return StatusCode(isValid.StatusCode);
        }catch(Exception e){
            return BadRequest(e.Message);
        }
    }

    // DELETE: api/prodotto/5
    [HttpDelete("saved/{id}")]
    public async Task<IActionResult> DeleteSavedProductAsync(Guid id, [FromHeader(Name = "Authentication")] string token)
    {
        try{
            MyStatusCodeResult isValid = _manager.ValidateAccessToken(token);
            if (isValid.StatusCode == 200)
            {
                string? role = _manager.ExtractRole(token); 
                string? sub = _manager.ExtractSub(token);
                
                if ( role != "utente" && role != "azienda")
                {
                    return Forbid();
                }
                else
                {
                    SavedProduct? savedProductToDelete = _context.SavedProduct.FirstOrDefault(savedProd => savedProd.ProdId == id && savedProd.UserId == sub);
                    if(savedProductToDelete != null){
                        _context.SavedProduct.Remove(savedProductToDelete);
                        await _context.SaveChangesAsync();
                    }
                    return Ok();
                }
            }
            return StatusCode(isValid.StatusCode);
        }catch(Exception e){
            return BadRequest(e.Message);
        }
    }


    // DELETE: api/prodotto/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProductAsync(Guid id, [FromHeader(Name = "Authentication")] string token)
    {
        try{
            MyStatusCodeResult isValid = _manager.ValidateAccessToken(token);
            if (isValid.StatusCode == 200)
            {
                string? sub = _manager.ExtractSub(token); 
                string? role = _manager.ExtractRole(token);
                
                if ( role != "utente" && role != "azienda")
                {
                    return Forbid();
                }
                else
                {
                    Console.WriteLine("DELETING PRODUCT");
                    Console.WriteLine(id);                
                    var prod = await _context.Product.FindAsync(id);

                    Console.WriteLine(prod?.Id);

                    if(prod != null && prod.Author == sub){    
                        if(prod.ProductImages != null && prod.ProductImages.Count > 0){
                            try{ 
                                //delete selcted image file from the cloud
                                var folderPath = $"products/{sub}/{id}";
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
                            }catch(AmazonS3Exception ex){
                                Console.WriteLine($"Error: '{ex.Message}'");  
                            }
                        }
                        _context.Product.Remove(prod);
                        await _context.SaveChangesAsync();
                    }
                }
            }

            return StatusCode(isValid.StatusCode);
        }catch(Exception e){
            return BadRequest(e.Message);
        } 
    }
    
    //IMAGES MANAGEMENT
    private string HmacHash(Guid? id)
    {
        string? secretKey = _configuration.GetValue<string>("SecretKey");
        string hash = "";
        if(secretKey != null && id != null){
            byte[] secretKeyBytes = Encoding.UTF8.GetBytes(secretKey);
            byte[] messageBytes = Encoding.UTF8.GetBytes(id.ToString()!);

            using HMACSHA256 hmac = new(secretKeyBytes);
            byte[] hashBytes = hmac.ComputeHash(messageBytes);
            hash = Convert.ToBase64String(hashBytes);
            string url_safe_id = _manager!.Encode(hash);

            Console.WriteLine("HMAC hash: " + hash);
            return url_safe_id;
        }
        return "";
    }
    private async Task UploadImageNameToDb(List<string> immaginiProduct, string sub, Guid? prodId){
        const string updateQuery = 
        """
            UPDATE Product SET product_images = @product_images WHERE id = @prod_id;
        """;
        await using var command = _conn.CreateCommand(updateQuery);
        command.Parameters.Add("@prod_id", NpgsqlDbType.Uuid).Value = prodId;
        command.Parameters.Add("@product_images", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = immaginiProduct;

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
                    newImages[i] = $"{_manager.Encode(HmacHash(Guid.NewGuid()))}";
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

    private async Task<List<string>> PostProductImagesToBucket(Product prod, List<IFormFile> files){
        //Get the uploaded images names (only 5 images per post are allowed)
        string sub = prod.Author!;
        int numberOfNewFiles = files.Count;
        int rangeToSelect = numberOfNewFiles > 5 ? 4 : numberOfNewFiles;
        var uploadedFileName = files.Select(file => file.FileName).ToList().GetRange(0, rangeToSelect);
        foreach (var file in files.GetRange(0, rangeToSelect))
        {

            var filename = _manager.Encode(HmacHash(Guid.NewGuid()))+".jpeg";
            Console.WriteLine($"FILENAME: {filename}");
            var imgPath = $"products/{sub}/{prod.Id}/{filename}";
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
                
                Console.WriteLine($"Error: '{ex.ErrorType} {ex.Message} {ex.ErrorCode}' when writing an object");
                return new List<string>();
            }
        }
        await UploadImageNameToDb(uploadedFileName, sub, prod.Id);
        //removeOldFiles(uploadedFiles, sub, prod.id);
        return uploadedFileName;
    }

    private async Task<List<string>> PutProductImagesToBucket(List<IFormFile> files, Product prod)
    {
        try
        {  
            //check if product exists
            if(prod != null){
                Console.WriteLine("PROD NOT NULL");
                
                //Get old images filenames from bucket
                ListObjectsV2Request listOldFilesRequest = new()
                {                            
                    BucketName = "fuocherello-bucket",
                    Prefix = $"products/{prod.Author}/{prod!.Id}",
                    StartAfter = $"products/{prod.Author}/{prod.Id}"
                }; 
                
                ListObjectsV2Response oldFiles = await _s3Client.ListObjectsV2Async(listOldFilesRequest);
                
                int numberOfOldFiles = oldFiles.S3Objects.Count;
                int numberOfNewFiles = files.Count;

                List<string> oldFileName = new();
                List<string> newFileName = new();

                if(numberOfOldFiles > 0){
                    Console.WriteLine("GETTING OLD IMAGES NAMES");
                    oldFileName = oldFiles.S3Objects.Select(file => ExtractImageNameFromPath(file.Key)).ToList();
                }else if(numberOfOldFiles == 0){
                    for(int i = 0; i < numberOfNewFiles && i < 5; i++){    
                        newFileName.Add($"{_manager.Encode(HmacHash(Guid.NewGuid()))}.jpeg");
                    }
                    await UploadProductImagesToBucket(prod.Author!, prod.Id.ToString()!, newFileName, files);
                    return newFileName;
                }

                if(numberOfNewFiles > 0){
                    newFileName = files.GetRange(0, numberOfNewFiles >= 5 ? 4 : numberOfNewFiles).Select(file => file.FileName).ToList();
                    numberOfNewFiles = newFileName.Count;
                }else if(numberOfNewFiles == 0){
                    Console.WriteLine("0 FILE RECEIVED");
                    Console.WriteLine($"{oldFileName}");
                    await DeleteProductImagesFromBucket(prod.Author!, prod.Id.ToString()!, oldFileName);
                    await UploadImageNameToDb(newFileName, prod.Author!, prod.Id);
                    return newFileName;
                }

                List<string> imagesToDelete = GetImagesToDelete(oldFileName, newFileName);
                await DeleteProductImagesFromBucket(prod.Author!, prod.Id.ToString()!, imagesToDelete);
            
                List<string> fileNamesToUpload = GetImagesToUpload(imagesToDelete, oldFileName, newFileName);
                await UploadProductImagesToBucket(prod.Author!, prod.Id.ToString()!, fileNamesToUpload, files);                  
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
