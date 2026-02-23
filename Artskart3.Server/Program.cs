using Azure.Identity;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddApplicationInsightsTelemetry(new Microsoft.ApplicationInsights.AspNetCore.Extensions.ApplicationInsightsServiceOptions
{
    ConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]
});

var app = builder.Build();

app.UseDefaultFiles();
app.MapStaticAssets();

app.Use(async (context, next) =>
{
    if (context.Request.Path == "/robots.txt")
    {
        context.Response.ContentType = "text/plain";
        
        if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Test"))
        {
            await context.Response.WriteAsync("User-agent: *\nDisallow: /\n");
        }
        else
        {
            await context.Response.WriteAsync("User-agent: *\nAllow: /\nCrawl-delay: 1\n");
        }
        return;
    }
    
    await next();
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
