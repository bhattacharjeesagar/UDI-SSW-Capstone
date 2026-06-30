using Microsoft.AspNetCore.Authentication.Cookies;
using UDI_SSW_Prototype.Data;
using UDI_SSW_Prototype.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register the Database Connection Factory (using Dapper/SqlConnection)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=(localdb)\\mssqllocaldb;Database=UDI_SSW_Prototype;Trusted_Connection=True;MultipleActiveResultSets=true";

builder.Services.AddSingleton<IDbConnectionFactory>(new SqlConnectionFactory(connectionString));

// Register Repositories
builder.Services.AddScoped<ICitizenRepository, CitizenRepository>();
builder.Services.AddScoped<ICredentialRepository, CredentialRepository>();
builder.Services.AddScoped<IKeyRegistryRepository, KeyRegistryRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

// Register Cryptographic Service
builder.Services.AddSingleton<ICryptoService, CryptoService>();

// Register Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(20);
        options.SlidingExpiration = true;
    });

var app = builder.Build();

// Automatically ensure the database, tables, and seed roles are created on startup
try
{
    using (var scope = app.Services.CreateScope())
    {
        var factory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        using var conn = factory.CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        
        // Create RoleMaster table
        cmd.CommandText = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RoleMaster')
            BEGIN
                CREATE TABLE RoleMaster (
                    RoleId INT PRIMARY KEY,
                    RoleName VARCHAR(50) NOT NULL
                );
            END";
        cmd.ExecuteNonQuery();

        // Create UserMaster table
        cmd.CommandText = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UserMaster')
            BEGIN
                CREATE TABLE UserMaster (
                    UserId INT IDENTITY(1,1) PRIMARY KEY,
                    Username VARCHAR(100) NOT NULL UNIQUE,
                    PasswordHash VARCHAR(256) NOT NULL,
                    RoleId INT NOT NULL,
                    FullName NVARCHAR(150) NOT NULL,
                    AssociatedDid VARCHAR(100) NULL,
                    IsActive BIT NOT NULL DEFAULT 1,
                    FOREIGN KEY (RoleId) REFERENCES RoleMaster(RoleId)
                );
            END";
        cmd.ExecuteNonQuery();

        // Create Citizens table
        cmd.CommandText = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Citizens')
            BEGIN
                CREATE TABLE Citizens (
                    Did VARCHAR(100) PRIMARY KEY,
                    Name NVARCHAR(150) NOT NULL,
                    Birthdate DATETIME NOT NULL,
                    LicenseNumber VARCHAR(50) NOT NULL,
                    PublicKeyPem VARCHAR(MAX) NOT NULL,
                    PrivateKeyPem VARCHAR(MAX) NOT NULL,
                    IsActive BIT NOT NULL DEFAULT 1,
                    LastActiveTime DATETIME NULL
                );
            END";
        cmd.ExecuteNonQuery();

        // Create Credentials table
        cmd.CommandText = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Credentials')
            BEGIN
                CREATE TABLE Credentials (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    SubjectDid VARCHAR(100) NOT NULL,
                    IssuerName VARCHAR(100) NOT NULL,
                    ClaimName VARCHAR(100) NOT NULL,
                    ClaimValue VARCHAR(50) NOT NULL,
                    SignedPayload VARCHAR(MAX) NOT NULL,
                    DocumentPath VARCHAR(255) NULL,
                    IsRevoked BIT NOT NULL DEFAULT 0,
                    IssuedAt DATETIME NOT NULL DEFAULT GETUTCDATE(),
                    FOREIGN KEY (SubjectDid) REFERENCES Citizens(Did)
                );
            END";
        cmd.ExecuteNonQuery();

        // Create Keys table
        cmd.CommandText = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Keys')
            BEGIN
                CREATE TABLE Keys (
                    IssuerName VARCHAR(100) PRIMARY KEY,
                    PublicKeyPem VARCHAR(MAX) NOT NULL,
                    PrivateKeyPem VARCHAR(MAX) NOT NULL,
                    IsActive BIT NOT NULL DEFAULT 1
                );
            END";
        cmd.ExecuteNonQuery();

        // Dynamically patch existing database schema for new columns if table already existed
        cmd.CommandText = @"
            IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Citizens')
            BEGIN
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Citizens') AND name = 'IsActive')
                BEGIN
                    ALTER TABLE Citizens ADD IsActive BIT NOT NULL DEFAULT 1;
                END
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Citizens') AND name = 'LastActiveTime')
                BEGIN
                    ALTER TABLE Citizens ADD LastActiveTime DATETIME NULL;
                END
            END

            IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Credentials')
            BEGIN
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Credentials') AND name = 'DocumentPath')
                BEGIN
                    ALTER TABLE Credentials ADD DocumentPath VARCHAR(255) NULL;
                END
            END

            IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Keys')
            BEGIN
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Keys') AND name = 'IsActive')
                BEGIN
                    ALTER TABLE Keys ADD IsActive BIT NOT NULL DEFAULT 1;
                END
            END
        ";
        cmd.ExecuteNonQuery();

        // Seed Roles
        cmd.CommandText = @"
            IF NOT EXISTS (SELECT * FROM RoleMaster WHERE RoleId = 1)
                INSERT INTO RoleMaster (RoleId, RoleName) VALUES (1, 'Issuer');
            IF NOT EXISTS (SELECT * FROM RoleMaster WHERE RoleId = 2)
                INSERT INTO RoleMaster (RoleId, RoleName) VALUES (2, 'Citizen');
            IF NOT EXISTS (SELECT * FROM RoleMaster WHERE RoleId = 3)
                INSERT INTO RoleMaster (RoleId, RoleName) VALUES (3, 'Verifier');
        ";
        cmd.ExecuteNonQuery();

        // Seed default users (Password hash is SHA256 of 'Test@123': '8776f108e247ab1e2b323042c049c266407c81fbad41bde1e8dfc1bb66fd267e')
        cmd.CommandText = @"
            IF NOT EXISTS (SELECT * FROM UserMaster WHERE Username = 'gov_issuer')
                INSERT INTO UserMaster (Username, PasswordHash, RoleId, FullName, AssociatedDid, IsActive)
                VALUES ('gov_issuer', '8776f108e247ab1e2b323042c049c266407c81fbad41bde1e8dfc1bb66fd267e', 1, 'National Identity Officer', NULL, 1);
            ELSE
                UPDATE UserMaster SET PasswordHash = '8776f108e247ab1e2b323042c049c266407c81fbad41bde1e8dfc1bb66fd267e' WHERE Username = 'gov_issuer';
            
            IF NOT EXISTS (SELECT * FROM UserMaster WHERE Username = 'verifier_user')
                INSERT INTO UserMaster (Username, PasswordHash, RoleId, FullName, AssociatedDid, IsActive)
                VALUES ('verifier_user', '8776f108e247ab1e2b323042c049c266407c81fbad41bde1e8dfc1bb66fd267e', 3, 'Eagle Car Rental Verifier', NULL, 1);
            ELSE
                UPDATE UserMaster SET PasswordHash = '8776f108e247ab1e2b323042c049c266407c81fbad41bde1e8dfc1bb66fd267e' WHERE Username = 'verifier_user';
        ";
        cmd.ExecuteNonQuery();

        // Dynamically seed default GovIdentityOffice cryptographic keys if Keys table is empty
        cmd.CommandText = "SELECT COUNT(*) FROM Keys";
        int keyCount = (int)cmd.ExecuteScalar();
        if (keyCount == 0)
        {
            var crypto = scope.ServiceProvider.GetRequiredService<ICryptoService>();
            var keys = crypto.GenerateKeyPair();
            cmd.CommandText = @"
                INSERT INTO Keys (IssuerName, PublicKeyPem, PrivateKeyPem, IsActive)
                VALUES ('GovIdentityOffice', @PublicKey, @PrivateKey, 1)";
            cmd.Parameters.Clear();
            cmd.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@PublicKey", keys.PublicKeyPem));
            cmd.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@PrivateKey", keys.PrivateKeyPem));
            cmd.ExecuteNonQuery();
        }
    }
}
catch (Exception ex)
{
    UDI_SSW_Prototype.Controllers.HomeController.DbInitError = ex.ToString();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication(); // Run Authentication before Authorization
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
