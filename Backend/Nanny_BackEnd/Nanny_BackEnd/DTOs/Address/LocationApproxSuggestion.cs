namespace Nanny_BackEnd.DTOs.Address;

public class LocationApproxSuggestion
{
    public string DisplayName { get; set; } = "";
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public string? Ward { get; set; }
}
