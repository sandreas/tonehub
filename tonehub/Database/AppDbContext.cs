using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using tonehub.Database.Models;

namespace tonehub.Database;

public class AppDbContext : DbContext
{

    // disable non nullable warning (I did not find a way to overcome this build warning)
#pragma warning disable 8618
    public DbSet<Setting> Settings { get; set; }

    public DbSet<Models.File> Files { get; set; }
    public DbSet<FileTag> FileTags { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<FileJsonValue> FileJsonValues { get; set; }

        
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {

    }
#pragma warning restore 8618
        
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new JsonConvertibleConfiguration<FileJsonValue, JToken>(vsi => vsi.Value));
        modelBuilder.ApplyConfiguration(new JsonConvertibleConfiguration<Setting, JToken>(vsi => vsi.Value));
        base.OnModelCreating(modelBuilder);
    }
        
    public override int SaveChanges()
    {
        var now = DateTime.UtcNow;

        foreach (var changedEntity in ChangeTracker.Entries())
        {
            if (changedEntity.Entity is ModelBase entity)
            {
                switch (changedEntity.State)
                {
                    case EntityState.Added:
                        entity.CreatedDate = now;
                        entity.UpdatedDate = now;
                        break;
                    case EntityState.Modified:
                        entity.UpdatedDate = now;
                        break;
                }
            }
        }

        return base.SaveChanges();

    }
}