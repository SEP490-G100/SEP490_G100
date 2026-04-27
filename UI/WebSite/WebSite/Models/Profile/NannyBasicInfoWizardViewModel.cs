using System.ComponentModel.DataAnnotations;

namespace WebSite.Models.Profile
{
    public class NannyBasicInfoWizardViewModel
    {
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "Số điện thoại")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "Ngày sinh")]
        public DateOnly? DateOfBirth { get; set; }

        [Display(Name = "Giới tính")]
        public int? Gender { get; set; }

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
        public string? AvatarUrl { get; set; }

        [Display(Name = "Ảnh đại diện")]
        public IFormFile? AvatarFile { get; set; }
    }
}
