using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

            Random random = new Random();
            string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            
            var randomChars = Enumerable.Repeat(chars, 32)
                .Select(s => s[random.Next(s.Length)]).ToArray();
            
            string key = new string(randomChars);

builder.Services.AddSingleton<IJwtAuthenticationManager>(new JwtAuthenticationManager(key));

builder.Configuration.AddEnvironmentVariables(prefix: "HPDS_");

// CORS policy
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(
        policy =>
        {
            policy.WithOrigins(builder.Configuration["HPDS_COMMON_FRONTEND_URL"])
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
builder.Services.AddDbContext<UserDb>(opt => opt.UseNpgsql(builder.Configuration["HPDS_DB_CONN_STRING"]));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// authentication
builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
});

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});


app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
    options.RoutePrefix = string.Empty;
});

app.UseAuthentication();


app.UseCors();

app.MapGet("/users", async (UserDb db) =>
    await db.Users.Select(x => new UserDTO(x)).ToListAsync())
.WithName("GetUsers");

app.MapGet("/users/{userName}", async (string userName, UserDb db) =>
    await db.Users.SingleOrDefaultAsync(x => x.Name == userName)
        is User user
            ? Results.Ok(new UserDTO(user))
            : Results.NotFound())
.WithName("GetUser");

app.MapPost("/users", async (PostUserDTO userDTO, UserDb db) =>
{
    var user = new User
    {
        Name = userDTO.Name
    };

    db.Users.Add(user);
    await db.SaveChangesAsync();

    Results.Created($"/users/{userDTO.Name}", new UserDTO(user));
})
.WithName("CreateUser");

app.MapDelete("/users/{userName}", async (string userName, UserDb db) =>
{
    if (await db.Users.SingleOrDefaultAsync(x => x.Name == userName) is User user)
    {
        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return Results.Ok(new UserDTO(user));
    }

    return Results.NotFound();
})
.WithName("DeleteUser");


app.MapPost("/authenticate", async (PostUserDTO userDTO, UserDb db) =>
{
        var user = new User
    {
        Name = userDTO.Name
    };

            Random random = new Random();
            string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            
            var randomChars = Enumerable.Repeat(chars, 32)
                .Select(s => s[random.Next(s.Length)]).ToArray();
            
            string key = new string(randomChars);
            IJwtAuthenticationManager jwtManager = new JwtAuthenticationManager(key);

    var token = jwtManager.Authenticate(userDTO.Name, db);
    if (token == null)
    {
        return Results.Unauthorized();
    }
    
    db.UserTokens.Add(new UserToken
    {
        Name = userDTO.Name,
        token = token
    });
    await db.SaveChangesAsync();

    return Results.Created($"/authenticate/{userDTO.Name}", new UserToken(user, token));
});



// Perform migrations at runtime
using (var scope = app.Services.CreateScope()) {
    var services = scope.ServiceProvider;

    var context = services.GetRequiredService<UserDb>();
    if (context.Database.GetPendingMigrations().Any())
    {
        context.Database.Migrate();
    }
}

app.Run();

public class User {
    public int Id { get; set; }

    public string Name { get; set; }
}


public class UserToken{
    public int Id { get; set; }

    public string Name { get; set; }

    public string token { get; set; }

    public UserToken() {}

    public UserToken(User user, string token) =>
        (Id, Name, token) = (user.Id, user.Name, token);

}

public class UserDTO {
    public int Id { get; set; }
    public string Name { get; set; }

    public UserDTO() { }
    public UserDTO(User user) =>
    (Id, Name) = (user.Id, user.Name);
}

public class PostUserDTO {
    public string Name { get; set; }

    public PostUserDTO() { }
    public PostUserDTO(User user) =>
    (Name) = (user.Name);
}

public class UserDb : DbContext
{
    public UserDb(DbContextOptions<UserDb> options)
        : base(options) { }
    public DbSet<User> Users => Set<User>();
    public DbSet<UserToken> UserTokens => Set<UserToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasAlternateKey(x => x.Name);
    }
}
