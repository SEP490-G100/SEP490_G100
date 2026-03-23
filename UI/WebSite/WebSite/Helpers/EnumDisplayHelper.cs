using WebSite.Enums;

namespace WebSite.Helpers;

public static class EnumDisplayHelper
{
    public static string GetDisplayName(EducationLevel level)
    {
        return level switch
        {
            EducationLevel.HighSchool => "Trung học",
            EducationLevel.College => "Cao đẳng",
            EducationLevel.Bachelor => "Đại học",
            EducationLevel.Master => "Thạc sĩ",
            EducationLevel.Other => "Khác",
            _ => level.ToString()
        };
    }

    public static string GetDisplayName(ProficiencyLevel level)
    {
        return level switch
        {
            ProficiencyLevel.Basic => "Cơ bản",
            ProficiencyLevel.Intermediate => "Trung cấp",
            ProficiencyLevel.Advanced => "Nâng cao",
            _ => level.ToString()
        };
    }

    public static string GetDisplayName(ChildAgeGroup group)
    {
        return group switch
        {
            ChildAgeGroup.Baby => "Em bé (0-1 tuổi)",
            ChildAgeGroup.Toddler => "Toddler (1-3 tuổi)",
            ChildAgeGroup.Preschooler => "Mầm non (3-5 tuổi)",
            ChildAgeGroup.Gradeschooler => "Học sinh tiểu học (6-12 tuổi)",
            _ => group.ToString()
        };
    }

    public static string GetDisplayName(VerificationStatus status)
    {
        return status switch
        {
            VerificationStatus.NotSubmitted => "Chưa gửi",
            VerificationStatus.Pending => "Đang chờ duyệt",
            VerificationStatus.Approved => "Đã duyệt",
            VerificationStatus.Rejected => "Bị từ chối",
            _ => status.ToString()
        };
    }

    public static List<(int Value, string Label)> GetEducationLevelOptions()
    {
        return new List<(int, string)>
        {
            ((int)EducationLevel.HighSchool, GetDisplayName(EducationLevel.HighSchool)),
            ((int)EducationLevel.College, GetDisplayName(EducationLevel.College)),
            ((int)EducationLevel.Bachelor, GetDisplayName(EducationLevel.Bachelor)),
            ((int)EducationLevel.Master, GetDisplayName(EducationLevel.Master)),
            ((int)EducationLevel.Other, GetDisplayName(EducationLevel.Other))
        };
    }

    public static List<(int Value, string Label)> GetProficiencyLevelOptions()
    {
        return new List<(int, string)>
        {
            ((int)ProficiencyLevel.Basic, GetDisplayName(ProficiencyLevel.Basic)),
            ((int)ProficiencyLevel.Intermediate, GetDisplayName(ProficiencyLevel.Intermediate)),
            ((int)ProficiencyLevel.Advanced, GetDisplayName(ProficiencyLevel.Advanced))
        };
    }

    public static List<(int Value, string Label)> GetChildAgeGroupOptions()
    {
        return new List<(int, string)>
        {
            ((int)ChildAgeGroup.Baby, GetDisplayName(ChildAgeGroup.Baby)),
            ((int)ChildAgeGroup.Toddler, GetDisplayName(ChildAgeGroup.Toddler)),
            ((int)ChildAgeGroup.Preschooler, GetDisplayName(ChildAgeGroup.Preschooler)),
            ((int)ChildAgeGroup.Gradeschooler, GetDisplayName(ChildAgeGroup.Gradeschooler))
        };
    }

    public static List<(int Value, string Label)> GetVerificationStatusOptions()
    {
        return new List<(int, string)>
        {
            ((int)VerificationStatus.NotSubmitted, GetDisplayName(VerificationStatus.NotSubmitted)),
            ((int)VerificationStatus.Pending, GetDisplayName(VerificationStatus.Pending)),
            ((int)VerificationStatus.Approved, GetDisplayName(VerificationStatus.Approved)),
            ((int)VerificationStatus.Rejected, GetDisplayName(VerificationStatus.Rejected))
        };
    }
}
