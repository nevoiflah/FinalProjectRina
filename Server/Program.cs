using FinalProjectRina.Server.BL;
using FinalProjectRina.Server.DAL;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure CORS with specific policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Register services
builder.Services.AddSingleton<IAiProvider, MockAiProvider>();
builder.Services.AddSingleton<ISpeechProvider, MockSpeechProvider>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<ISpeechService, SpeechService>();
builder.Services.AddScoped<IUserService, UserService>();

var app = builder.Build();

app.UseCors("AllowAll");

app.UseRouting();

app.MapControllers();

var urls = app.Urls;
app.Logger.LogInformation("Server is running on: {Urls}", string.Join(", ", urls));
app.Logger.LogInformation("CORS is enabled for all origins");

app.Run();