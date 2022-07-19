using Microsoft.EntityFrameworkCore;
using tonehub.Database;

namespace tonehub.Services;

public class DatabaseSettingsService
{
    private readonly AppDbContext _db;

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
        var setting = _db.Settings.FirstOrDefault(s => s.Key == key);
        return setting == null ? default : setting.Value.ToObject<T>();
    }
}