using RobotsTxt;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddStaticRobotsTxt(options =>
{
    if (!builder.Environment.IsProduction())
        options.DenyAll();
    else
        options.AddSection(section => section
            .AddUserAgent("*")
            .Allow("/")
            .AddCrawlDelay(TimeSpan.FromSeconds(1)));

    return options;
});

var app = builder.Build();

app.UseDefaultFiles();
app.MapStaticAssets();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapControllers();

app.MapFallbackToFile("/index.html");

app.Run();
