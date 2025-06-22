using Microsoft.EntityFrameworkCore;
using src_api.Data;
using src_api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Entity Framework com SQLite
builder.Services.AddDbContext<ChatBotContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// CORS para permitir Angular
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// HttpClient para IAs
builder.Services.AddHttpClient("ChatGPT", client =>
{
    client.BaseAddress = new Uri("https://api.openai.com/");
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {builder.Configuration["OpenAI:ApiKey"]}");
});

builder.Services.AddHttpClient("Claude", client =>
{
    client.BaseAddress = new Uri("https://api.anthropic.com/");
    client.DefaultRequestHeaders.Add("x-api-key", builder.Configuration["Claude:ApiKey"]);
    client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
});

builder.Services.AddHttpClient("LMStudio", client =>
{
    client.BaseAddress = new Uri("http://192.168.0.150:1234/");
});

// Adiciona o serviço da base de conhecimento
builder.Services.AddSingleton<KnowledgeBaseService>();

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "ChatBot API", Version = "v1" });
});

var app = builder.Build();

// Configurar pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Aplicar migrations automaticamente
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ChatBotContext>();
    context.Database.EnsureCreated();
}

app.UseCors("AllowAngular");
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
