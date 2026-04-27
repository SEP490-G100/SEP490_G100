namespace WebSite.Models.Profile
{
    using System.ComponentModel.DataAnnotations;
    using WebSite.Enums;

    public class UpdateChildProfileViewModel
    {
        public Guid Id { get; set; }
        [Display(Name = "Nhu cầu đặc biệt")]
        [StringLength(1000)]
        public string? SpecialNeeds { get; set; }
        [Display(Name = "Ghi chú")]
        [StringLength(1000)]
        public string? Notes { get; set; }
        [Display(Name = "Đặc điểm")]
        [StringLength(1000)]
        public string? Characteristic { get; set; }
        [Display(Name = "Nhóm tuổi của trẻ")]
        [Required(ErrorMessage = "Vui lòng chọn nhóm tuổi của trẻ.")]
        [EnumDataType(typeof(ChildAgeGroup))]
        public ChildAgeGroup? ChildAgeGroup { get; set; }
    }
}
