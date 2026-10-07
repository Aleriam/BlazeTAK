using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BlazeTAK.Data.Models;

[Table("Profiles")]
[Index(nameof(ApplicationUserId), IsUnique = true)]
public class User : VirtUser
{
    //link to the Identity login account
    [ForeignKey(nameof(ApplicationUser))]
    public string ApplicationUserId { get; set; } = null!;
    public ApplicationUser ApplicationUser { get; set; } = null!;
    
    //unit the user is a member of
    [ForeignKey(nameof(Unit))]
    public Guid? UnitId { get; set; }
    [DeleteBehavior(DeleteBehavior.SetNull)]
    [InverseProperty(nameof(Models.Unit.Users))]
    public Unit? Unit { get; set; }
    
    [ForeignKey(nameof(Group))]
    
    public Guid? GroupId { get; set; }
    [DeleteBehavior(DeleteBehavior.SetNull)]
    
    public Group? Group { get; set; }

    //group user requested/opted to join
    [ForeignKey(nameof(RequestedGroup))]
    public Guid? RequestedGroupId { get; set; }
    [DeleteBehavior(DeleteBehavior.SetNull)]
    public Group? RequestedGroup { get; set; }

    //simple permissions for reading data of other units in group
    public bool ReadGroupData { get; set; } = true;
    public bool EditGroupData { get; set; } = false;
    
    //add location, only in ram not saved
    [NotMapped]
    public NetTopologySuite.Geometries.Point? Location { get; set; }
}
