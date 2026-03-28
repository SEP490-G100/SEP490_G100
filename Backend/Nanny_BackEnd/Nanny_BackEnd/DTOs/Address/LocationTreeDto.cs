namespace Nanny_BackEnd.DTOs.Address;

public class ProvinceLocationDto
{
    public int Code { get; set; }
    public string Name { get; set; } = "";
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public List<DistrictLocationDto> Districts { get; set; } = [];
}

public class DistrictLocationDto
{
    public int Code { get; set; }
    public string Name { get; set; } = "";
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public List<WardLocationDto> Wards { get; set; } = [];
}

public class WardLocationDto
{
    public int Code { get; set; }
    public string Name { get; set; } = "";
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}
