using Microsoft.EntityFrameworkCore;
using Fuocherello.Data;
using System.Net;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Security.Cryptography;
using Fuocherello.Services.EmailService;
using Npgsql;
using Fuocherello.Singleton.JwtManager;

var builder = WebApplication.CreateBuilder(args);
//DB connection
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
string ConnString = builder.Configuration.GetConnectionString("DbContext")!;
builder.Services.AddDbContext<ApiServerContext>(options => options.UseNpgsql(ConnString));
await using var dataSource = NpgsqlDataSource.Create(ConnString);
builder.Services.AddSingleton(dataSource);

string filename = "key";
using (RSA rsa = RSA.Create())
{
    byte[] privateKeyBytes = rsa.ExportRSAPrivateKey();
    File.WriteAllBytes(filename, privateKeyBytes);
}

RSA rsaKey = RSA.Create();
rsaKey.ImportRSAPrivateKey(File.ReadAllBytes("key"), out _);

builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddSingleton<IJwtManager>(new JwtManager(rsaKey));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers().AddNewtonsoftJson();

//deeplinks json auth file
string jsonFilePath = $"{Path.GetFullPath(".")}/assetlinks.json";

//builder.Services.AddSingleton(rsaKey);

builder.Services.AddAuthentication("jwt")
    .AddJwtBearer("jwt", o => {
        o.MapInboundClaims = false;
        o.Configuration = new OpenIdConnectConfiguration(){
            SigningKeys = {
                new RsaSecurityKey(rsaKey)
            }
        };
    });

var app = builder.Build();

// Middleware to handle IP address redirection
app.Use((context, next) =>
{
    // Check if the request is made using an IP address
    if (context.Request.Host.Host.Equals(context.Request.Host.Value))
    {
        // Construct the URL using your domain name
        var newUrl = $"https://www.zophirel.it:8443";

        // Perform redirection
        context.Response.Redirect(newUrl, permanent: true);
        return Task.CompletedTask;
    }

    return next();
});

app.UseRouting();

//to serve cleint dynamic links
app.MapGet("/",  (HttpContext context) => "OK");

//to serve cleint dynamic links
app.MapGet("/.well-known/assetlinks.json", async (HttpContext context) =>
{
    var fileInfo = new FileInfo(jsonFilePath);
    
    if (!fileInfo.Exists)
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsync("File not found");
        return;
    }

    context.Response.ContentType = "application/json";
   await context.Response.SendFileAsync(fileInfo.FullName);
});

var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromMinutes(2)
};
app.UseWebSockets(webSocketOptions);

if (!builder.Environment.IsDevelopment())
{
   
    builder.Services.AddHttpsRedirection(options =>
    {
        options.RedirectStatusCode = (int)HttpStatusCode.PermanentRedirect;
        options.HttpsPort = 443;
    });
}
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else 
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
