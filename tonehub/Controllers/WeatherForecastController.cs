using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tonehub.Database;
using tonehub.Services;

namespace tonehub.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    private readonly ILogger<WeatherForecastController> _logger;
    private readonly AppDbContext _db;
    private readonly DatabaseSettingsService _settings;

    public WeatherForecastController(DatabaseSettingsService settings, ILogger<WeatherForecastController> logger)
    {
        _logger = logger;
        _settings = settings;
    }
/*
    [HttpGet(Name = "GetWeatherForecast")]
    public IEnumerable<WeatherForecast> Get()
    {
        return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
    }
    */
    [HttpGet(Name = "GetWeatherForecast")]
    public ActionResult<string> Get()
    {
        var value = _settings.Get<string>("testing");
        return Ok(value);
    }
    
}