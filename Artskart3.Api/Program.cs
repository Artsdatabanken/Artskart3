var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseDefaultFiles();
app.MapStaticAssets();

app.Use((context, next) =>
{
    if (context.Request.Path == "/robots.txt")
    {
        context.Response.ContentType = "text/plain";
        
        if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Test"))
        {
            context.Response.WriteAsync("User-agent: *\nDisallow: /\n");
        }
        else
        {
            context.Response.WriteAsync("User-agent: *\nAllow: /\nCrawl-delay: 1\n");
        }
    }
    
    return next();
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapControllers();

app.MapFallbackToFile("/index.html");

app.Run();
