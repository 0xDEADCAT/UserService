using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
builder.Services.AddDbContext<UserDb>(opt => opt.UseNpgsql("Host=localhost;Port=5432;Database=userdb;Username=postgres;Password=postgres"));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
    options.RoutePrefix = string.Empty;
});


app.MapGet("/users", async (UserDb db) =>
    await db.Users.Select(x => new UserDTO(x)).ToListAsync())
.WithName("GetUsers");

app.MapGet("/users/{id}", async (int id, UserDb db) =>
    await db.Users.FindAsync(id)
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

    return Results.Created($"/users/{user.Id}", new UserDTO(user));
})
.WithName("CreateUser");

app.MapDelete("/users/{id}", async (int id, UserDb db) =>
{
    if (await db.Users.FindAsync(id) is User user)
    {
        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return Results.Ok(new UserDTO(user));
    }

    return Results.NotFound();
})
.WithName("DeleteUser");

app.Run();

public class User {
    public int Id { get; set; }
    public string? Name { get; set; }
}

public class UserDTO {
    public int Id { get; set; }
    public string? Name { get; set; }

    public UserDTO() { }
    public UserDTO(User user) =>
    (Id, Name) = (user.Id, user.Name);
}

public class PostUserDTO {
    public string? Name { get; set; }

    public PostUserDTO() { }
    public PostUserDTO(User user) =>
    (Name) = (user.Name);
}

public class UserDb : DbContext
{
    public UserDb(DbContextOptions<UserDb> options)
        : base(options) { }
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<User>()
        .HasKey(c => new { c.Id, c.Name });
}
}