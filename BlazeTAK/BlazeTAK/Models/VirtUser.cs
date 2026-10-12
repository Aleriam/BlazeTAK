using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlazeTAK.Data.Models;

[Table("VirtUsers")]
public class VirtUser
{
    [Key]
    public Guid ID { get; set; }

    [MaxLength(256)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(64)]
    public string CallSign { get; set; } = string.Empty;
    
    public int CallSignNumber { get; set; }
}
