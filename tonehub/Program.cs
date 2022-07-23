using System.IO.Abstractions;
using JsonApiDotNetCore.Configuration;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sandreas.Files;
using tonehub.Database;
using tonehub.HostedServices;
using tonehub.Metadata;
using tonehub.Options;
using tonehub.Services;
using tonehub.Settings;

var builder = WebApplication.CreateBuilder(args);
// https://docs.microsoft.com/de-de/aspnet/core/fundamentals/configuration/?view=aspnetcore-6.0
// ToneHub__DatabaseUri
builder.Configuration.AddEnvironmentVariables();
builder.Services.Configure<ToneHubOptions>(builder.Configuration.GetSection("ToneHub"));
builder.Services.AddDbContextFactory<AppDbContext>((services, options) =>
    {
        var settings = services.GetRequiredService<IOptions<ToneHubOptions>>();
        
        var connectionString = Utility.UriToConnectionString(settings.Value.DatabaseUri);
        switch(settings.Value.DatabaseUri.Scheme){
            case "sqlite":
                options.UseSqlite(connectionString);
                break;
            case "pgsql":
                options.UseNpgsql(connectionString);
                // https://stackoverflow.com/questions/69961449/net6-and-datetime-problem-cannot-write-datetime-with-kind-utc-to-postgresql-ty/70142836#70142836
                AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
                break;
        }
        
    }
);
// Add services to the container.
builder.Services.AddSingleton<DatabaseSettingsService>();
builder.Services.AddSingleton<FileSystem>();
builder.Services.AddSingleton<FileWalker>();
builder.Services.AddSingleton<AudioFileLoader>();
builder.Services.AddSingleton(s => new FileIndexerSettings
{
    DeleteOrphansAfter = TimeSpan.FromSeconds(86400)
});
builder.Services.AddSingleton<FileIndexerService>();
builder.Services.AddSingleton(_ => new FileExtensionContentTypeProvider
{
    Mappings =
    {
        [".m4b"] = "audio/x-m4b"
    }
});


builder.Services.AddJsonApi<AppDbContext>(options => options.Namespace = "api");
// builder.Services.AddJsonApi<AppDbContext>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// background services, e.g. FileIndexer
builder.Services.AddHostedService<BackgroundFileIndexerService>();

var app = builder.Build();
var contextFactory = app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
await using (var db = await contextFactory.CreateDbContextAsync())
{
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.UseRouting();

app.UseJsonApi();

app.MapControllers();

// await CreateDatabaseAsync(app.Services);

app.Run();

/*
static async Task CreateDatabaseAsync(IServiceProvider serviceProvider)
{
    await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.EnsureCreatedAsync();

    if (!dbContext.Settings.Any())
    {
        dbContext.Settings.Add(new Setting
        {
            Key = "John Doe"
        });

        await dbContext.SaveChangesAsync();
    }
}
*/