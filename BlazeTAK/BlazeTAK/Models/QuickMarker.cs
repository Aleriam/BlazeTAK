using NetTopologySuite.Geometries;

namespace BlazeTAK.Data.Models;

public class QuickMarker
{
    //used for quick marking, stored only in ram no database
    public User CreatedBy { get; set; } = null!;
    public int EnemyNumber { get; set; }
    //TODO add position
    public Point? Location { get; set; }
}