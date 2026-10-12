using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BlazeTAK.Data.Models;

[Table("Markers")]
public class Marker
{
    [Key]
    public Guid Id { get; set; }

    [MaxLength(256)] public string Name { get; set; } = string.Empty;
    public DateTime TimeStamp_Created { get; set; } //always UTC (Npgsql timestamptz)
    public DateTime TimeStamp_Updated { get; set; } //always UTC (Npgsql timestamptz)
    
    [MaxLength(256)]
    public string? Description { get; set; }
    [MaxLength(64)]
    public string? UnitType { get; set; }//will be used to assigne image in UI
    
    //nullable so deleting a user account keeps the marker
    [ForeignKey(nameof(CreatedBy))]
    public Guid? CreatedById { get; set; }
    [DeleteBehavior(DeleteBehavior.SetNull)]
    public VirtUser? CreatedBy { get; set; }

    [ForeignKey(nameof(UpdatedBy))]
    public Guid? UpdatedById { get; set; }
    [DeleteBehavior(DeleteBehavior.SetNull)]
    public VirtUser? UpdatedBy { get; set; }

    [ForeignKey(nameof(Unit))]
    public Guid? UnitId { get; set; }
    [DeleteBehavior(DeleteBehavior.Cascade)]
    [InverseProperty(nameof(Models.Unit.Markers))]
    public Unit? Unit { get; set; }
}
