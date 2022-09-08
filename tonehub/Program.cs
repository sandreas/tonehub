using System.Diagnostics;
using System.IO.Abstractions;
using System.Reflection;
using JsonApiDotNetCore.Configuration;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Sandreas.Files;
using Serilog;
using Serilog.Core;
using tonehub.Database;
using tonehub.HostedServices;
using tonehub.HostedServices.Scoped;
using tonehub.Metadata;
using tonehub.Options;
using tonehub.Services;
using tonehub.Settings;


// this does not work because of JsonApiDotNet (https://github.com/json-api-dotnet/JsonApiDotNetCore/issues/1082)
// solution: see catch block
/*
Log.Logger = new LoggerConfiguration()
    // .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateBootstrapLogger(); // creates a default ReloadableLogger that can be overridden (see https://nblumhardt.com/2020/10/bootstrap-logger/)
*/
WebApplication? app = null;
try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Configuration.AddEnvironmentVariables();

    builder.Logging.ClearProviders();
    // configure and override defaults with extensive logging (see https://gist.github.com/Alger23/bb848e902a5347d072d9853f79475159)
    builder.Host.UseSerilog((context, services, loggerConfiguration) =>
    {
        loggerConfiguration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom
            .Services(services) // see https://github.com/serilog/serilog-aspnetcore#injecting-services-into-enrichers-and-sinks
            // via appsettings.json
            // .Enrich.FromLogContext()
            // .Enrich.WithExceptionDetails()
            // .Enrich.WithMachineName() 
            // .Enrich.WithEnvironmentName()
            // .Enrich.WithClientIp()
            // .Enrich.WithClientAgent()
            .Enrich.WithProperty("Application", context.HostingEnvironment.ApplicationName)
            .Enrich.WithProperty("Version", Assembly.GetAssembly(typeof(Program))?
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? "unknown")
            ;
#if DEBUG
        loggerConfiguration.Enrich.WithProperty("DebuggerAttached", Debugger.IsAttached);
#endif
    });


    // https://docs.microsoft.com/de-de/aspnet/core/fundamentals/configuration/?view=aspnetcore-6.0
    // ToneHub__DatabaseUri
    builder.Services.Configure<ToneHubOptions>(builder.Configuration.GetSection("ToneHub"));
    builder.Services.AddDbContextFactory<AppDbContext>((services, options) =>
        {
            var settings = services.GetRequiredService<IOptions<ToneHubOptions>>();

            var connectionString = Utility.UriToConnectionString(settings.Value.DatabaseUri);
            switch (settings.Value.DatabaseUri.Scheme)
            {
                case "sqlite":
                    options.UseSqlite(connectionString);
                    break;
                case "pgsql":
                    options.UseNpgsql(connectionString, o =>
                    {
                        o.EnableRetryOnFailure(maxRetryCount: 4, maxRetryDelay: TimeSpan.FromSeconds(1),
                            errorCodesToAdd: Array.Empty<string>());
                    });
                    // same like AsNoTracking() but by default (see https://docs.microsoft.com/en-us/ef/core/querying/tracking#configuring-the-default-tracking-behavior)
                    // options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
                    if (builder.Environment.IsDevelopment())
                    {
                        options.EnableDetailedErrors();
                        options.EnableSensitiveDataLogging();
                        options.ConfigureWarnings(warningsActions =>
                        {
                            warningsActions.Log(new EventId[]
                            {
                                CoreEventId.FirstWithoutOrderByAndFilterWarning,
                                CoreEventId.RowLimitingOperationWithoutOrderByWarning
                            });
                        });
                    }

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
    builder.Services.AddSingleton(_ => new FileIndexerSettings
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
    // builder.Services.AddHostedService<BackgroundFileIndexerService>();
    builder.Services.AddHostedService<ConsumeScopedFileIndexerServiceHostedService>();
    builder.Services.AddScoped<IScopedFileIndexerService, ScopedFileIndexerService>();

    app = builder.Build();
    var contextFactory = app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using (var db = await contextFactory.CreateDbContextAsync())
    {
        db.Database.Migrate();
    }

    // Write streamlined request completion events, instead of the more verbose ones from the framework.
    // To use the default framework request logging instead, remove this line and set the "Microsoft"
    // level in appsettings.json to "Information".
    app.UseSerilogRequestLogging();


    // Configure the HTTP request pipeline.

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
            options.RoutePrefix = "api";
        });
    }

    app.UseHttpsRedirection();
    app.UseDefaultFiles();
    app.UseStaticFiles();

    app.UseAuthorization();


    app.UseRouting();

    app.UseJsonApi();

    app.MapControllerRoute(
        name: "default",
        pattern: "api/{controller}/{action}/{id?}"
    );

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
}
catch (Exception ex)
{
    if (Log.Logger == null || Log.Logger == Logger.None)
    {
        var loggerConfig = new LoggerConfiguration();
        loggerConfig = app != null
            ? loggerConfig.ReadFrom.Configuration(app.Configuration)
            : loggerConfig.MinimumLevel.Debug();
        Log.Logger = loggerConfig
            .WriteTo.Console()
            .CreateLogger();
    }

    Log.Fatal(ex, "host terminated due to uncaught exception");
}
finally
{
    Log.CloseAndFlush();
}