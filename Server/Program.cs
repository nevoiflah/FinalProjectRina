using FinalProjectRina.Server.BL;
using FinalProjectRina.Server.DAL;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddCors();

builder.Services.AddSingleton<IAiProvider, MockAiProvider>();
builder.Services.AddSingleton<ISpeechProvider, MockSpeechProvider>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<ISpeechService, SpeechService>();
builder.Services.AddSingleton<IUserService, UserService>();

var app = builder.Build();

app.UseCors(policy => policy
    .AllowAnyOrigin()
    .AllowAnyHeader()
    .AllowAnyMethod());

app.MapControllers();

app.Run();
