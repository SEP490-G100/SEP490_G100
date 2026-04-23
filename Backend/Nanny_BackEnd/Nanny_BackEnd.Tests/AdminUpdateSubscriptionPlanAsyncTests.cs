using System.Text.Json;
using Moq;
using Nanny_BackEnd.DTOs.Subscription;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// <see cref="Nanny_BackEnd.Controllers.AdminSubcriptionPlanController"/> update →
/// <see cref="AdminSubcriptionPlanService.AdminUpdateSubscriptionPlanAsync"/>.
/// </summary>
public class AdminUpdateSubscriptionPlanAsyncTests
{
    private readonly Mock<IAdminSubcriptionPlanRepository> _mockRepo;
    private readonly AdminSubcriptionPlanService _sut;

    public AdminUpdateSubscriptionPlanAsyncTests()
    {
        _mockRepo = new Mock<IAdminSubcriptionPlanRepository>();
        _sut = new AdminSubcriptionPlanService(_mockRepo.Object);
    }

    private static string FeaturesJson(string code, string targetRole) =>
        JsonSerializer.Serialize(new SubscriptionPlanMetadata
        {
            Code = code,
            TargetRole = targetRole,
            Features = new List<string> { "old" },
            Benefits = new SubscriptionBenefitResponse
            {
                MonthlyJobPostLimit = 0,
                MonthlyApplicationLimit = 0,
                FeaturedBadge = false,
                SearchPriority = false,
                ListingDurationDays = 0,
                CanUseRecommendation = false
            }
        });

    private static AdminSubscriptionPlanUpsertRequest ValidRequest() => new()
    {
        Name = "Gói Premium",
        Description = "Mô tả cập nhật",
        TargetRole = "Nanny",
        Price = 120_000m,
        DurationDays = 60,
        SortOrder = 7,
        Features = new List<string> { "Năng 1" },
        Benefits = new AdminSubscriptionPlanBenefitRequest
        {
            MonthlyJobPostLimit = 0,
            MonthlyApplicationLimit = 10,
            FeaturedBadge = false,
            SearchPriority = true,
            ListingDurationDays = 0
        },
        CanUseRecommendation = false
    };

    // Condition: validation trước khi tìm gói.
    [Fact]
    public async Task ValidationFails_Throws_DoesNotQueryPlan()
    {
        var req = ValidRequest();
        req.Name = "Y";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.AdminUpdateSubscriptionPlanAsync(Guid.NewGuid(), Guid.NewGuid(), req));

        Assert.NotNull(ex.Message);
        _mockRepo.Verify(r => r.FindAdminPlanByIdIncludingDeletedAsync(It.IsAny<Guid>()), Times.Never);
    }

    // Condition: feature chỉ còn khoảng trắng (Validate trước khi tìm gói).
    [Fact]
    public async Task Features_WhitespaceOnly_Throws()
    {
        var req = ValidRequest();
        req.Features = new List<string> { "  " };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.AdminUpdateSubscriptionPlanAsync(Guid.NewGuid(), Guid.NewGuid(), req));

        Assert.Equal("Phai co it nhat 1 feature cho goi subscription.", ex.Message);
        _mockRepo.Verify(r => r.FindAdminPlanByIdIncludingDeletedAsync(It.IsAny<Guid>()), Times.Never);
        _mockRepo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    // Condition: không tìm thấy gói theo id.
    [Fact]
    public async Task NotFound_ThrowsKeyNotFound()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.FindAdminPlanByIdIncludingDeletedAsync(id)).ReturnsAsync((SubscriptionPlan?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.AdminUpdateSubscriptionPlanAsync(id, Guid.NewGuid(), ValidRequest()));

        Assert.Equal("Khong tim thay goi subscription.", ex.Message);
    }

    // Condition: tên (trim) trùng gói khác — ExistsPlanName với excludeId = id gói đang sửa.
    [Fact]
    public async Task DuplicateName_Throws()
    {
        var id = Guid.NewGuid();
        var plan = new SubscriptionPlan
        {
            Id = id,
            Name = "A",
            Features = FeaturesJson("A", "Parent")
        };
        _mockRepo.Setup(r => r.FindAdminPlanByIdIncludingDeletedAsync(id)).ReturnsAsync(plan);
        _mockRepo.Setup(r => r.ExistsPlanNameIncludingDeletedAsync("Gói Premium", id)).ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.AdminUpdateSubscriptionPlanAsync(id, Guid.NewGuid(), ValidRequest()));

        Assert.Equal("Ten goi subscription da ton tai.", ex.Message);
        _mockRepo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    // Condition: cập nhật thành công; SortOrder lấy từ request; không gọi GetNext/Add.
    [Fact]
    public async Task Update_Success_MutatesPlan_ReturnsDetail()
    {
        var id = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var createdAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var plan = new SubscriptionPlan
        {
            Id = id,
            Name = "Tên cũ",
            Description = "Cũ",
            Features = FeaturesJson("OLD", "Parent"),
            Price = 10_000m,
            DurationDays = 7,
            SortOrder = 1,
            IsActive = true,
            IsDeleted = false,
            CanUseRecommendation = true,
            CreatedAt = createdAt
        };
        _mockRepo.Setup(r => r.FindAdminPlanByIdIncludingDeletedAsync(id)).ReturnsAsync(plan);
        _mockRepo.Setup(r => r.ExistsPlanNameIncludingDeletedAsync("Bản  Mới", id)).ReturnsAsync(false);
        _mockRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _mockRepo.Setup(r => r.CountActiveSubscriptionsByPlanAsync(id, It.IsAny<DateTime>())).ReturnsAsync(3);

        var req = ValidRequest();
        req.Name = "  Bản  Mới  ";
        req.Description = "  mới  ";
        req.SortOrder = 7;

        var r = await _sut.AdminUpdateSubscriptionPlanAsync(id, adminId, req);

        Assert.Equal("Bản  Mới", plan.Name);
        Assert.Equal("mới", plan.Description);
        Assert.Equal(120_000m, plan.Price);
        Assert.Equal(60, plan.DurationDays);
        Assert.Equal(7, plan.SortOrder);
        Assert.False(plan.CanUseRecommendation);
        Assert.Equal(adminId, plan.UpdatedBy);
        Assert.NotNull(plan.UpdatedAt);
        var stored = SubscriptionPlanMetadataHelper.TryParse(plan.Features);
        Assert.NotNull(stored);
        Assert.Equal("Nanny", stored!.TargetRole);
        Assert.Contains("Năng 1", stored.Features, StringComparer.Ordinal);

        Assert.Equal(id, r.Id);
        Assert.Equal("Bản  Mới", r.Name);
        Assert.Equal(7, r.SortOrder);
        Assert.Equal(120_000m, r.Price);
        Assert.Equal(3, r.ActiveSubscriberCount);
        Assert.Equal(createdAt, r.CreatedAt);
        Assert.Equal("Nanny", r.TargetRole);
        Assert.Equal(10, r.Benefits.MonthlyApplicationLimit);

        _mockRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
        _mockRepo.Verify(r => r.GetNextSubscriptionPlanSortOrderAsync(), Times.Never);
        _mockRepo.Verify(r => r.AddPlan(It.IsAny<SubscriptionPlan>()), Times.Never);
        _mockRepo.Verify(
            x => x.CountActiveSubscriptionsByPlanAsync(id, It.IsAny<DateTime>()),
            Times.Once);
    }

    // Condition: mô tả rỗng — lưu null.
    [Fact]
    public async Task Update_EmptyDescription_StoresNull()
    {
        var id = Guid.NewGuid();
        var plan = new SubscriptionPlan
        {
            Id = id,
            Name = "Gói A",
            Features = FeaturesJson("A", "Parent")
        };
        _mockRepo.Setup(r => r.FindAdminPlanByIdIncludingDeletedAsync(id)).ReturnsAsync(plan);
        _mockRepo.Setup(r => r.ExistsPlanNameIncludingDeletedAsync("Gói A", id)).ReturnsAsync(false);
        _mockRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _mockRepo.Setup(r => r.CountActiveSubscriptionsByPlanAsync(It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .ReturnsAsync(0);
        var req = ValidRequest();
        req.Name = "Gói A";
        req.Description = "   ";

        var r = await _sut.AdminUpdateSubscriptionPlanAsync(id, Guid.NewGuid(), req);

        Assert.Null(plan.Description);
        Assert.Null(r.Description);
    }
}
