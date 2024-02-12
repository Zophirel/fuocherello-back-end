# Fuocherello Server
This is a REST API server meant to be used by the Fuocherello Flutter App (Client), it provides all the routes needed to perfom CRUD operations, data is stored using PostrgresSQL

## Table of Contents
- [Overview](#overview)
- [Features](#features)
- [How to build](#how-to-build)
- [Dependencies](#dependencies)
- [Contributing](#contributing)
- [License](#license)

## Overview
An MVC project mainly composed by models, controllers and a custom helper the view part is provided by the client app

## Features
- **HTTPS:** Configured to use Lets encrypt ssl certificates
- **Authentication:** Allows the users to log in, log out and sign up
- **Oauth Authentication:** Allows the users to log in, log out and sign up using google as auth provider
- **Editing User Profile :**	Allows users to edit the data provided during the sign up process
- **Publishing Products** Allows users to ***publish, edit and delete*** their selling announcments
- **Saving Products :**	Allows users to save the selling announcments they are intrested in
- **Searching Products :**	Allows users to search for selling announcments from an input field
- **S3 Configured:** The server uses an S3 client to manage all the image files sent by the client
- **Dynamic Links:** Allows the user to open the client app from an external link
- **Email:** Send an email to verify users when signign up or when they need to replace their password


## How to build
	├── /bin
	├── /Controllers
	├── /Data
	├── /Models
	├── /obj
	├── /Properties
	├── /Services
	├── /Singleton
	├── Program.cs
	├── appsettings.Development.json      (𝗣𝗿𝗲𝘀𝗲𝗻𝘁 𝗯𝘂𝘁 𝗶𝗻𝗰𝗼𝗺𝗽𝗹𝗲𝘁𝗲)
	├── appsettings.json                             (𝗣𝗿𝗲𝘀𝗲𝗻𝘁 𝗯𝘂𝘁 𝗶𝗻𝗰𝗼𝗺𝗽𝗹𝗲𝘁𝗲)
	├── assetlinks.json                                 (𝗥𝗲𝗽𝗹𝗮𝗰𝗲 𝘁𝗼 𝘂𝘀𝗲 𝗼𝘁𝗵𝗲𝗿 𝗱𝘆𝗻𝗮𝗺𝗶𝗰 𝗹𝗶𝗻𝗸𝘀)
	├── key                                                  (𝗺𝗶𝘀𝘀𝗶𝗻𝗴)
	└── sslCertificate.pfx                             (𝗺𝗶𝘀𝘀𝗶𝗻𝗴)

### 1. [appsettings.json](https://github.com/Zophirel/fuocherello-back-end/blob/main/appsettings.json "appsettings.json") - [appsettings.Development.json](https://github.com/Zophirel/fuocherello-back-end/blob/main/appsettings.Development.json "appsettings.Development.json")
In these files is specified the configuration needed to connect the server with:

1. SSL certificate
2. Email SMTP
3. S3 Storage
4. PostgreSQL
	
 ```
{
  "Kestrel": {

  (SSL certificate)
  "Certificates": {
    "Default": {
    "Path": "SslCertificate.pfx",
    "Password": ""
    }
  }
  },
  "https_port": 443,

  (email smtp credentials)
  "EmailHost": "",
  "EmailUsername": "",
  "EmailPassword": "", 
  (key used for encrypting string with Hmac enc.)
  "SecretKey" : "",

  (S3 stroage credentials)
  "S3awsAccessKeyId" : "",
  "S3awsSecretAccessKey" : "",

  (Password used to send data from this server to the SignalR one)
  "SignalRServerPassword" : "",
  "Logging": {
  "LogLevel": {
    "Default": "Information",
    "Microsoft.AspNetCore": "Warning"
  }
  },

  (PostgreSQL connection string)
  "AllowedHosts": "*",
  "ConnectionStrings": {
  "DbContext": "Username=user; Password=pass; Database=dbname; Host=dbip; Port=5432;"
  }
}
```
### 2. key (missing file)
`key` is an RSA private key saved locally and used for signing JWT tokens and OAuth signup / logins (both usage can be found in the [Porgram.cs](https://github.com/Zophirel/fuocherello-back-end/blob/main/Program.cs "Porgram.cs") file)

To generate the key you need to run this code in Progam.cs

	string filename = "key";
	using (RSA rsa = RSA.Create())
	{
		byte[] privateKeyBytes = rsa.ExportRSAPrivateKey();
		File.WriteAllBytes(filename, privateKeyBytes);
	}

I tried generating keys with Python but it seems it has some compatibility issues 

### 3. SslCertificate.pfx (missing file)
if you use windows, the SSL certifate shouldn't be a problem since it should be auto generated, i did the project from linux because the server that is hosting the code is a linux server, i used [cerbot](https://certbot.eff.org/ "cerbot") to get a certificate.

once certbot is installed <br>

1. Generate a certificate with:
 
<ul>
  <li>
    <p><code>certbot certonly --manual --preferred-challenges dns --email administrator@domain.com --domains domain.com`</code></p>
  </li>
</ul>


2. Convert the certificate in pfx format
<ul>
  <li> 
    <p><code>openssl pkcs12 -export -out certificate_fullchain.pfx -inkey privkey.pem -in fullchain.pem</code></p>
  	<p><code>`privkey.pem`</code> can be found in <code>`/etc/letsencrypt/live/example.com/privkey.pem`</code></p>
    <p><code>`fullchain.pem`</code> can be found in <code>`/etc/letsencrypt/live/example.com/fullchain.pem`</code></p>
  </li>
</ul>

3. Edit the the appsettings.json file to import the certificate


### 4. [assetlinks.json](https://github.com/Zophirel/fuocherello-back-end/blob/main/assetlinks.json "assetlinks.json") (optional)

An example on how to edit this file for using your links can be found [Here](https://firebase.google.com/support/guides/app-links-universal-links#app-links "Here"), this wont be enough to make the App Links work, the client side needs to be modified too

## Contributing

Contributions are welcome! Feel free to open issues or submit pull requests.

## License

This project is licensed under the [MIT License](LICENSE).
