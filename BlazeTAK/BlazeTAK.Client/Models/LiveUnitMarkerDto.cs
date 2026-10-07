namespace BlazeTAK.Client.Models;

public class LiveUnitMarkerDto
{
    public string Id { get; set; } = string.Empty; // Unique key for the cluster/marker
    public Guid UnitId { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public string CallSign { get; set; } = string.Empty;
    public int CallSignNumber { get; set; }
    public string UnitType { get; set; } = "SFGPUCI----"; // SIDC
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int MemberCount { get; set; }
    public int MarkerRadius { get; set; }
    public bool IsLeaderMarker { get; set; }
    public List<Guid> MemberIds { get; set; } = new();
    public List<string> MemberNames { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
