namespace WebSite.Models.Nanny;

public class NannySkillSelectionViewModel
{
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public int? ProficiencyLevel { get; set; }
}

public class NannySkillsViewModel
{
    public List<NannySkillSelectionViewModel> AvailableSkills { get; set; } = new();
    public List<NannySkillSelectionViewModel> SelectedSkills { get; set; } = new();
}

