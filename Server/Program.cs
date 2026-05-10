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
        SeedKnowledge(db);
        userService?.PromoteUserToAdmin("nevo.iflah6@gmail.com");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Warning: Startup DB tasks failed (DB may be unreachable): {ex.Message}");
    }
}

app.UseRouting();
app.UseCors("AllowAll");
app.MapControllers();

app.Run("http://localhost:5102");

static void SeedKnowledge(IMongoDatabase db)
{
    var col = db.GetCollection<KnowledgeFact>("ruppinKnowledge");
    if (col.CountDocuments(Builders<KnowledgeFact>.Filter.Empty) > 0) return;

    col.InsertMany(new[]
    {
        new KnowledgeFact { Category = "Faculties", FactText = "Ruppin Academic Center offers Bachelor's and Master's degrees across four main faculties: Faculty of Management & Economics, Faculty of Engineering, Faculty of Social & Community Sciences, and Faculty of Marine Sciences." },
        new KnowledgeFact { Category = "Faculties, Marine", FactText = "The Faculty of Marine Sciences is located at the Mikhmoret campus. It provides BSc degrees in Marine Biotechnology and Marine Sciences/Environment, and MSc/MA degrees. Ruppin is the only academic institution in Israel granting Bachelor's degrees in marine sciences." },
        new KnowledgeFact { Category = "Admissions, General", FactText = "General admission requires a high school Bagrut diploma for Bachelor's programs and a Bachelor's degree for Master's programs. Specific thresholds apply per degree." },
        new KnowledgeFact { Category = "Admissions, Computer Science", FactText = "Admission to Computer Science requires a Weighted Average of 105+ and a Math score of 90+ in 5 units." },
        new KnowledgeFact { Category = "Admissions, Engineering", FactText = "Admission to Engineering (Electrical/Industrial) requires a Weighted Average of 100+ and a Math score of 80+ in 4 or 5 units." },
        new KnowledgeFact { Category = "Admissions, Nursing", FactText = "Admission to the Nursing (BSN) program specifically requires a Psychometric exam score of 550+ and passing an interview." },
        new KnowledgeFact { Category = "Admissions, Mechina", FactText = "Students missing required grades can enroll in the Pre-Academic Preparatory Program (Mechina) to replace their missing grades and improve their chances of acceptance." },
        new KnowledgeFact { Category = "Scholarships", FactText = "Ruppin offers various merit-based scholarships. Eligible local students can receive up to $1,200 per year." },
        new KnowledgeFact { Category = "Scholarships, Marine", FactText = "The Faculty of Marine Sciences provides a full-tuition (100%) scholarship for the first year of BSc studies for students entering with a psychometric exam score of 690 or higher." },
        new KnowledgeFact { Category = "Scholarships, Logistics", FactText = "Scholarships are granted in collaboration with the Sachish family and the Shipping Administration for outstanding projects and Master's theses in Logistics and Global Supply Chain." },
        new KnowledgeFact { Category = "Dorms, Housing", FactText = "The Ruppin Educational Center provides 400 dorm rooms for students. While on-campus housing is limited, the center offers assistance in finding off-campus housing in Emek Hefer." },
        new KnowledgeFact { Category = "Tuition", FactText = "Estimated annual tuition is subsidized for specific BSc programs. Note that rates vary based on exact degree choices." }
    });
}
