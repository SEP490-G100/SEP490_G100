using System.ComponentModel.DataAnnotations;

namespace WebSite.Models.Profile
{
    public class ParentOnboardingWizardViewModel
    {
        // Step 1
        [Display(Name = "Họ và tên")]
        public string? FullName { get; set; }

        [Display(Name = "Số điện thoại")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "Ngày sinh")]
        public DateOnly? DateOfBirth { get; set; }

        // Step 2
        [Display(Name = "Địa chỉ chi tiết")]
        public string? Address { get; set; }

        [Display(Name = "Tỉnh / Thành phố")]
        public string? City { get; set; }

        [Display(Name = "Quận / Huyện / Phường")]
        public string? District { get; set; }

        [Display(Name = "Phường / Xã")]
        public string? Ward { get; set; }

        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }

        // Step 3
        public string? AvatarUrl { get; set; }

        [Display(Name = "Ảnh đại diện")]
        public IFormFile? AvatarFile { get; set; }

        // Step 4
        [Display(Name = "Mô tả gia đình")]
        public string? FamilyDescription { get; set; }

        [Display(Name = "Số lượng trẻ")]
        public int? NumberOfChildren { get; set; }

        // Step 5 - Children
        public List<ParentOnboardingChildInputViewModel> Children { get; set; } = new()
        {
            new ParentOnboardingChildInputViewModel()
        };
    }

    public class ParentOnboardingChildInputViewModel
    {
        [Display(Name = "Nhóm tuổi của trẻ")]
        public WebSite.Enums.ChildAgeGroup? ChildAgeGroup { get; set; }

        [Display(Name = "Nhu cầu đặc biệt")]
        public string? ChildSpecialNeeds { get; set; }

        [Display(Name = "Đặc điểm")]
        public string? ChildCharacteristic { get; set; }

        [Display(Name = "Ghi chú")]
        public string? ChildNotes { get; set; }
    }
}
