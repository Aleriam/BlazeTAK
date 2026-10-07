namespace BlazeTAK.Client.Models;

public class MarkerDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string UnitType { get; set; } = "SFGPUCI----"; // Default SIDC e.g. Friendly Infantry
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime TimeStamp_Created { get; set; } = DateTime.UtcNow;
    public DateTime TimeStamp_Updated { get; set; } = DateTime.UtcNow;
    public Guid? UnitId { get; set; }
    public Guid? GroupId { get; set; }
    public Guid? CreatedById { get; set; }
    public string? CreatedByName { get; set; }
}
