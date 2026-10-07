namespace BlazeTAK.Client.Models;

public class MarkerCategory
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "bi-tag";
    public List<MarkerSymbolOption> Symbols { get; set; } = new();
}

public class MarkerSymbolOption
{
    public string Name { get; set; } = string.Empty;
    public string Sidc { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CategoryId { get; set; } = string.Empty;
}
