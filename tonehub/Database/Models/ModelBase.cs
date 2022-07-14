using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;

namespace tonehub.Database.Models;

public class ModelBase: Identifiable<Guid>
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public override Guid Id { get; set; }

    [Attr] public DateTimeOffset CreatedDate { get; set; }


    [Attr] public DateTimeOffset UpdatedDate { get; set; }
    
}