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
    
    public T? Get<T>(string key)
    {
        var setting = _db.Settings.FirstOrDefault(s => s.Key == key);
        return setting == null ? default : setting.Value.ToObject<T>();
    }
}