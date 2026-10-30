using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;

namespace BlazeTAK.Data.Models;

[Table("Groups")]
public class Group : VirtUser
{
    //all data is shared in a group and is accesible
    //eg data about units etc is accesible by members - by permission
    //admin-appointed group leader - may send messages on behalf of the whole group (chat) and later edit group info
    [ForeignKey(nameof(Leader))]
    public Guid? LeaderId { get; set; }
    [DeleteBehavior(DeleteBehavior.SetNull)]
    [InverseProperty(nameof(Models.User.LeadingGroup))]
    public User? Leader { get; set; }

    [InverseProperty(nameof(Unit.Group))]
    public List<Unit> Units { get; set; } = new();
}
