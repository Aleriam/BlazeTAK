using System.ComponentModel.DataAnnotations.Schema;
using NetTopologySuite.Geometries;

namespace BlazeTAK.Data.Models;

public class NonPersistentMarker : Marker
{
    [NotMapped]
    public Point? Location { get; set; }
}