using Nanny_BackEnd.Enums;

namespace Nanny_BackEnd.Helpers;

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
            VerificationStatus.Approved => "Đã xác minh danh tính",
            VerificationStatus.Rejected => "Bị từ chối",
            _ => status.ToString()
        };
    }
}
