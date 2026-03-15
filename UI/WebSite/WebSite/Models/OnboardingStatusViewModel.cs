namespace WebSite.Models;

public class OnboardingStatusViewModel
{
    public bool RequiresOnboarding { get; set; }
    public string Role { get; set; } = string.Empty;
    public string NextStep { get; set; } = "Completed";
}

