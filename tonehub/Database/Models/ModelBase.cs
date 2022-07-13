namespace tonehub.Controllers;

public class ModelBase: Identifiable<Guid>
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public override Guid Id { get; set; }

    [Attr] public DateTimeOffset CreatedDate { get; set; }


    [Attr] public DateTimeOffset UpdatedDate { get; set; }
{
    
}