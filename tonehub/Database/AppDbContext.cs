using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using tonehub.Database.Models;

namespace tonehub.Database;

public class AppDbContext : DbContext
{

    // disable non nullable warning (I did not find a way to overcome this build warning)
#pragma warning disable 8618

    public DbSet<Setting> Settings { get; set; }
    public DbSet<DataItem> DataItems { get; set; }
    public DbSet<FileJsonValue> FileJsonValues { get; set; }
    public DbSet<FileSource> FileSources { get; set; }
    public DbSet<Models.File> Files { get; set; }
    public DbSet<FileTag> FileTags { get; set; }
    public DbSet<SmartFileList> SmartFileLists { get; set; }
    public DbSet<StaticFileList> StaticFileLists { get; set; }
    public DbSet<Tag> Tags { get; set; }


    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {

    }
#pragma warning restore 8618
        
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new JsonConvertibleConfiguration<FileJsonValue, JToken>(vsi => vsi.Value));
        modelBuilder.ApplyConfiguration(new JsonConvertibleConfiguration<Setting, JToken>(vsi => vsi.Value));
        modelBuilder.ApplyConfiguration(new JsonConvertibleConfiguration<DataItem, JToken>(vsi => vsi.Value));
        modelBuilder.ApplyConfiguration(new JsonConvertibleConfiguration<SmartFileList, JToken>(vsi => vsi.Query));
        
        // does not work with sqlite
        // modelBuilder.Entity<Setting>().Property(p => p.Disabled).HasDefaultValue(false);
        
        base.OnModelCreating(modelBuilder);
    }
        
    public override int SaveChanges()
    {
        var now = DateTimeOffset.UtcNow;
        var changedEntities = ChangeTracker.Entries().ToList();
        foreach (var changedEntity in changedEntities)
        {
            if (changedEntity.Entity is ModelBaseDated entity)
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
        /*
        // this does not seem to help
        var result = base.SaveChanges();
        foreach (var entityEntry in ChangeTracker.Entries())
        {
            entityEntry.State = EntityState.Detached;
        }
        GC.Collect();
        return result;
        */
    }
    
    /*
    // https://stackoverflow.com/questions/30209528/memory-leak-when-using-entity-framework
    // does not work
    */
    
    public void Detach(object entity) 
    {
        
    
    }
    /*
    public override void Dispose()
    {
        
        var changedEntities = ChangeTracker.Entries().ToList();
        var count = changedEntities.Count();
        //foreach (var entityEntry in changedEntities)
        //{
         //   entityEntry.State = EntityState.Detached;
        //}
        
        ChangeTracker.Clear();
        base.Dispose();
        GC.Collect();


    }*/
    
    
}