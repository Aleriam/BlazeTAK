namespace BlazeTAK.Client.Models;

public class UserLocationDto
{
    public Guid UserId { get; set; }
    public Guid? UnitId { get; set; }
    public Guid? GroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CallSign { get; set; } = string.Empty;
    public int CallSignNumber { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? Heading { get; set; }
    public double? Speed { get; set; }
    public double? Accuracy { get; set; }
    public bool IsLeader { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
