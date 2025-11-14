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

builder.Services.AddScoped<IAiProvider, OpenAiChatProvider>();
builder.Services.AddScoped<ISpeechProvider, OpenAiSpeechProvider>();

// Business Logic Services
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<ISpeechService, SpeechService>();
builder.Services.AddScoped<IUserService, UserService>();

var app = builder.Build();

app.UseCors("AllowAll");
app.UseRouting();
app.MapControllers();

app.Run();