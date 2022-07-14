using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace tonehub.Database;

public class JsonConvertibleConfiguration<TEntity, TProperty> : IEntityTypeConfiguration<TEntity> where TEntity : class where TProperty: notnull
{
    private readonly Expression<Func<TEntity,TProperty>> _propertyExpression;

    public JsonConvertibleConfiguration(Expression<Func<TEntity, TProperty>> propertyExpression)
    {
        _propertyExpression = propertyExpression;
    }
    
    public void Configure(EntityTypeBuilder<TEntity> builder)
    {
        var serializerSettings = new JsonSerializerSettings {NullValueHandling = NullValueHandling.Ignore};
        // This Converter will perform the conversion to and from Json to the desired type
        builder.Property(_propertyExpression).HasConversion(
                v => JsonConvert.SerializeObject(v, serializerSettings),
                v => (JsonConvert.DeserializeObject<TProperty>(v, serializerSettings) ?? default(TProperty))!)
            .Metadata.SetValueComparer(new ValueComparer<JToken>(
                (c1, c2) => JToken.DeepEquals(c1, c2),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c));
    }
}