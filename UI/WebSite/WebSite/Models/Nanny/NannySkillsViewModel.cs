namespace WebSite.Models.Nanny;

using WebSite.Enums;

public class NannySkillSelectionViewModel
{
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public ProficiencyLevel? ProficiencyLevel { get; set; }
}

public class NannySkillsViewModel
{
    public List<NannySkillSelectionViewModel> AvailableSkills { get; set; } = new();
    public List<Guid> SelectedSkillIds { get; set; } = new();
}
