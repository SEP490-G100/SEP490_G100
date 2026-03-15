namespace WebSite.Models.Nanny;

public class DayAvailabilityViewModel
{
    public int DayOfWeek { get; set; }
    public string DayName { get; set; } = string.Empty;
    public bool Morning { get; set; }
    public bool Afternoon { get; set; }
    public bool Evening { get; set; }
    public bool Night { get; set; }
}

public class NannyAvailabilityViewModel
{
    public List<DayAvailabilityViewModel> Days { get; set; } = new()
    {
        new() { DayOfWeek = 1, DayName = "Thứ 2" },
        new() { DayOfWeek = 2, DayName = "Thứ 3" },
        new() { DayOfWeek = 3, DayName = "Thứ 4" },
        new() { DayOfWeek = 4, DayName = "Thứ 5" },
        new() { DayOfWeek = 5, DayName = "Thứ 6" },
        new() { DayOfWeek = 6, DayName = "Thứ 7" },
        new() { DayOfWeek = 0, DayName = "Chủ nhật" }
    };
}

