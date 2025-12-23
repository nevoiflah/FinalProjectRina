using FinalProjectRina.Server.BL;
using FinalProjectRina.Server.DAL;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// HttpClient for OpenAI API calls
builder.Services.AddHttpClient();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});


builder.Services.AddScoped<IAiProvider, PythonAiProvider>();
builder.Services.AddScoped<ISpeechProvider, OpenAiSpeechProvider>();

// Business Logic Services
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<ISpeechService, SpeechService>();
builder.Services.AddScoped<IUserService, UserService>();

var app = builder.Build();

// Ensure DB Schema and Promote Admin
using (var scope = app.Services.CreateScope())
{
    var userService = scope.ServiceProvider.GetRequiredService<IUserService>() as UserService;
    // Trigger schema creation via constructor or explicit call if needed (it's in ctor)
    userService?.PromoteUserToAdmin("nevo.iflah6@gmail.com");
}

app.UseRouting();
app.UseCors("AllowAll");
app.MapControllers();

app.Run();