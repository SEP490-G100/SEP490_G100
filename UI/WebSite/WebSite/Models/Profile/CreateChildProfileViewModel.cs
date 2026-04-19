namespace WebSite.Models.Profile
{
    using System.ComponentModel.DataAnnotations;
    using WebSite.Enums;

    public class CreateChildProfileViewModel
    {
        [StringLength(1000)]
        public string? SpecialNeeds { get; set; }
        [StringLength(1000)]
        public string? Notes { get; set; }
        [StringLength(1000)]
        public string? Characteristic { get; set; }
        [Required(ErrorMessage = "Vui l?ng chon nhom tuoi cua tre.")]
        [EnumDataType(typeof(ChildAgeGroup))]
        public ChildAgeGroup? ChildAgeGroup { get; set; }
    }
}
