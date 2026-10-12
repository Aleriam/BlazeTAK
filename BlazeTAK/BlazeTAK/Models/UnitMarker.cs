using System.ComponentModel.DataAnnotations.Schema;
using NetTopologySuite.Geometries;

namespace BlazeTAK.Data.Models;

public class UnitMarker : Marker
{
    //will save positionn of unit, ussually only one,
    //could be more if team splits on map
    //(uint represents number of members on that location) 
    [NotMapped]
    //multiple instances of markers, sync between clients live
    public List<KeyValuePair<uint, Point>> Markers { get; set; } = new();
}