using Moq;
using Nanny_BackEnd.DTOs.Subscription;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// <see cref="Nanny_BackEnd.Controllers.AdminSubcriptionPlanController.AdminCreateSubscriptionPlan"/> →
/// <see cref="AdminSubcriptionPlanService.AdminCreateSubscriptionPlanAsync"/>.
/// </summary>
public class AdminCreateSubscriptionPlanAsyncTests
{
    private readonly Mock<IAdminSubcriptionPlanRepository> _mockRepo;
    private readonly AdminSubcriptionPlanService _sut;

    public AdminCreateSubscriptionPlanAsyncTests()
    {
        _mockRepo = new Mock<IAdminSubcriptionPlanRepository>();
        _sut = new AdminSubcriptionPlanService(_mockRepo.Object);
    }

    private static AdminSubscriptionPlanUpsertRequest ValidRequest() => new()
    {
        Name = "Gói Premium",
        Description = "Mô tả gói",
        TargetRole = "Parent",
        Price = 50_000m,
        DurationDays = 30,
        SortOrder = 99,
        Features = new List<string> { "Tính năng A", "B" },
        Benefits = new AdminSubscriptionPlanBenefitRequest
        {
            MonthlyJobPostLimit = 5,
            MonthlyApplicationLimit = 0,
            FeaturedBadge = true,
            SearchPriority = true,
            ListingDurationDays = 30
        },
        CanUseRecommendation = true
    };

    // Condition: dữ liệu không thỏa [Required] / [StringLength] / [Range] — ném trước khi gọi repo tên trùng.
    [Fact]
    public async Task ValidationFails_ThrowsInvalidOperation()
    {
        var req = ValidRequest();
        req.Name = "X";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.AdminCreateSubscriptionPlanAsync(Guid.NewGuid(), req));

        Assert.NotNull(ex.Message);
        _mockRepo.Verify(r => r.ExistsPlanNameIncludingDeletedAsync(It.IsAny<string>(), It.IsAny<Guid?>()), Times.Never);
    }

    // Condition: mảng feature còn phần tử nhưng sau chuẩn hóa thành rỗng.
    [Fact]
    public async Task Features_WhitespaceOnly_Throws()
    {
        var req = ValidRequest();
        req.Features = new List<string> { "   " };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.AdminCreateSubscriptionPlanAsync(Guid.NewGuid(), req));

        Assert.Equal("Phai co it nhat 1 feature cho goi subscription.", ex.Message);
        _mockRepo.Verify(r => r.AddPlan(It.IsAny<SubscriptionPlan>()), Times.Never);
    }

    // Condition: tên gói (trim) đã tồn tại kể cả bản ghi xóa mềm.
    [Fact]
    public async Task DuplicateName_Throws()
    {
        _mockRepo.Setup(r => r.ExistsPlanNameIncludingDeletedAsync("Gói Premium", null)).ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.AdminCreateSubscriptionPlanAsync(Guid.NewGuid(), ValidRequest()));

        Assert.Equal("Ten goi subscription da ton tai.", ex.Message);
        _mockRepo.Verify(r => r.GetNextSubscriptionPlanSortOrderAsync(), Times.Never);
    }

    // Condition: tạo mới, sort lấy từ GetNext; request.SortOrder bị bỏ qua; gọi SaveChanges; trả về detail.
    [Fact]
    public async Task Create_Success_AddsPlan_ReturnsDetail()
    {
        var adminId = Guid.NewGuid();
        var req = ValidRequest();
        req.Name = "  Gói Mới  ";
        req.Description = "  mô tả  ";

        SubscriptionPlan? added = null;
        _mockRepo.Setup(r => r.ExistsPlanNameIncludingDeletedAsync("Gói Mới", null)).ReturnsAsync(false);
        _mockRepo.Setup(r => r.GetNextSubscriptionPlanSortOrderAsync()).ReturnsAsync(4);
        _mockRepo.Setup(r => r.AddPlan(It.IsAny<SubscriptionPlan>()))
            .Callback<SubscriptionPlan>(p => added = p);
        _mockRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _mockRepo.Setup(r => r.FindAdminPlanByIdIncludingDeletedAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => added != null && added.Id == id ? added : null);
        _mockRepo.Setup(r => r.CountActiveSubscriptionsByPlanAsync(It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .ReturnsAsync(0);

        var r = await _sut.AdminCreateSubscriptionPlanAsync(adminId, req);

        Assert.NotNull(added);
        Assert.Equal("Gói Mới", added.Name);
        Assert.Equal("mô tả", added.Description);
        Assert.Equal(50_000m, added.Price);
        Assert.Equal(30, added.DurationDays);
        Assert.Equal(4, added.SortOrder);
        Assert.True(added.IsActive);
        Assert.False(added.IsDeleted);
        Assert.Equal(adminId, added.CreatedBy);
        Assert.NotNull(added.Features);
        var stored = SubscriptionPlanMetadataHelper.TryParse(added.Features);
        Assert.NotNull(stored);
        Assert.Equal("Parent", stored!.TargetRole);
        Assert.Contains("Tính năng A", stored.Features, StringComparer.Ordinal);
        Assert.True(added.CanUseRecommendation);

        Assert.Equal(added.Id, r.Id);
        Assert.Equal("Gói Mới", r.Name);
        Assert.Equal("mô tả", r.Description);
        Assert.Equal(4, r.SortOrder);
        Assert.Equal(50_000m, r.Price);
        Assert.Equal(30, r.DurationDays);
        Assert.Equal("Parent", r.TargetRole);
        Assert.Equal(0, r.ActiveSubscriberCount);
        Assert.True(r.CanUseRecommendation);
        Assert.NotEmpty(r.Features);
        Assert.Equal(5, r.Benefits.MonthlyJobPostLimit);
        Assert.True(r.Benefits.FeaturedBadge);

        _mockRepo.Verify(r => r.GetNextSubscriptionPlanSortOrderAsync(), Times.Once);
        _mockRepo.Verify(r => r.AddPlan(It.IsAny<SubscriptionPlan>()), Times.Once);
        _mockRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
        _mockRepo.Verify(
            x => x.CountActiveSubscriptionsByPlanAsync(added.Id, It.IsAny<DateTime>()),
            Times.Once);
    }

    // Condition: mô tả rỗng — lưu null.
    [Fact]
    public async Task Create_EmptyDescription_StoresNull()
    {
        var req = ValidRequest();
        req.Description = "   ";
        _mockRepo.Setup(r => r.ExistsPlanNameIncludingDeletedAsync(It.IsAny<string>(), null)).ReturnsAsync(false);
        _mockRepo.Setup(r => r.GetNextSubscriptionPlanSortOrderAsync()).ReturnsAsync(1);
        SubscriptionPlan? added = null;
        _mockRepo.Setup(r => r.AddPlan(It.IsAny<SubscriptionPlan>()))
            .Callback<SubscriptionPlan>(p => added = p);
        _mockRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _mockRepo.Setup(r => r.FindAdminPlanByIdIncludingDeletedAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => added != null && added.Id == id ? added : null);
        _mockRepo.Setup(r => r.CountActiveSubscriptionsByPlanAsync(It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .ReturnsAsync(0);

        var r = await _sut.AdminCreateSubscriptionPlanAsync(Guid.NewGuid(), req);

        Assert.Null(added!.Description);
        Assert.Null(r.Description);
    }
}
