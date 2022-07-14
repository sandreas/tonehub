using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tonehub.Database;
using tonehub.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<ToneHubOptions>(builder.Configuration.GetSection("ToneHub"));
builder.Services.AddDbContextFactory<AppDbContext>((services, options) =>
    {
        var settings = services.GetRequiredService<IOptions<ToneHubOptions>>();
        var connectionString = Utility.UriToConnectionString(settings.Value.DatabaseUri);
        options.UseSqlite(connectionString);
    }
);
// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
var contextFactory = app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
await using(var db = await contextFactory.CreateDbContextAsync()){
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

app.MapControllers();

app.Run();
