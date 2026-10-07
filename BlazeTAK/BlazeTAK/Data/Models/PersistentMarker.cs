using System.ComponentModel.DataAnnotations.Schema;
using NetTopologySuite.Geometries;

namespace BlazeTAK.Data.Models;

public class PersistentMarker : Marker
{
    [Column(TypeName = "geometry(Point, 4326)")]
    public Point Location { get; set; } = null!;
}