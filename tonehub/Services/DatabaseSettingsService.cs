using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using tonehub.Database;
using tonehub.Database.Models;

namespace tonehub.Services;

public class DatabaseSettingsService
{
    private readonly AppDbContext _db;
    private Dictionary<string, JToken?> defaultValues = new()    {
        {"mediaPath", null},
        {"deleteOrphansAfterSeconds", new JValue(86400)},
    };

    public DatabaseSettingsService(IDbContextFactory<AppDbContext> db)
    {
        _db = db.CreateDbContext();
    }
    
    public bool TryGet<T>(string key, out T? value)
    {
        value = default;
        try
        {
            value = Get<T>(key);
            return true;
        }
        catch (Exception e)
        {
            return false;
        }
    }
    
    public T? Get<T>(string key)
    {
        var setting = _db.Settings.FirstOrDefault(s => s.Key == key && !s.Disabled);
        if(setting == null && defaultValues.ContainsKey(key) && defaultValues[key] != null)
        {
            setting = new Setting
            {
                Key = key,
                Value = defaultValues[key] ?? new JObject()
            };
        }
        return setting == null ? default : setting.Value.ToObject<T>();
    }
}