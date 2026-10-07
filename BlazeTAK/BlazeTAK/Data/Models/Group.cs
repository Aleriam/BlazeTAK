using System.ComponentModel.DataAnnotations.Schema;

namespace BlazeTAK.Data.Models;

[Table("Groups")]
public class Group : VirtUser
{
    //all data is shared in a group and is accesible
    //eg data about units etc is accesible by members - by permission
    [InverseProperty(nameof(Unit.Group))]
    public List<Unit> Units { get; set; } = new();
}
