using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BlazeTAK.Data.Models;

[Table("Units")]
public class Unit : VirtUser
{
    [ForeignKey(nameof(Group))]
    public Guid? GroupId { get; set; }
    [DeleteBehavior(DeleteBehavior.SetNull)]
    [InverseProperty(nameof(Models.Group.Units))]
    public Group? Group { get; set; }

    [ForeignKey(nameof(Leader))]
    public Guid? LeaderId { get; set; }
    [DeleteBehavior(DeleteBehavior.SetNull)]
    public User? Leader { get; set; }

    [InverseProperty(nameof(User.Unit))]
    public List<User> Users { get; set; } = new();

    [InverseProperty(nameof(Marker.Unit))]
    public List<Marker> Markers { get; set; } = new();

    public int MarkerRaduis { get; set; } //in meters, when all users are in that radius around leader they are under one marker
    [System.ComponentModel.DataAnnotations.MaxLength(64)]
    public string? UnitType { get; set; } = "SFGPUCI----";
    //TODO coordinates of leader?
    [NotMapped]
    public NetTopologySuite.Geometries.Point? Location { get; set; }
    [NotMapped]
    public UnitMarker? Marker { get; set; }
    
}
