using FinalProjectRina.Server.BL;
using FinalProjectRina.Server.DAL;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddHttpClient();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// MongoDB
builder.Services.AddSingleton<IMongoClient>(sp =>
    new MongoClient(builder.Configuration.GetConnectionString("MongoDB")));
builder.Services.AddSingleton<IMongoDatabase>(sp =>
    sp.GetRequiredService<IMongoClient>()
        .GetDatabase(builder.Configuration["MongoDB:DatabaseName"] ?? "FinalProjectRina"));

builder.Services.AddScoped<IAiProvider, PythonAiProvider>();
builder.Services.AddScoped<ISpeechProvider, OpenAiSpeechProvider>();

builder.Services.AddSingleton<KnowledgeCache>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<ISpeechService, SpeechService>();
builder.Services.AddScoped<IUserService, UserService>();

var app = builder.Build();

// Seed knowledge and promote admin
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
    var userService = scope.ServiceProvider.GetRequiredService<IUserService>() as UserService;
    try
    {
        await KnowledgeSeeder.SeedAsync(db, app.Configuration["OpenAI:ApiKey"] ?? "");
        userService?.PromoteUserToAdmin("nevo.iflah6@gmail.com");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Warning: Startup DB tasks failed (DB may be unreachable): {ex.Message}");
    }
}

app.UseCors("AllowAll");
app.UseRouting();
app.MapControllers();

app.Run("http://localhost:5102");
