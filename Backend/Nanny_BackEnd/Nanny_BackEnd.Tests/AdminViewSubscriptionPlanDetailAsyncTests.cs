using System.Text.Json;
using Moq;
using Nanny_BackEnd.DTOs.Subscription;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// <see cref="Nanny_BackEnd.Controllers.AdminSubcriptionPlanController"/> detail →
/// <see cref="AdminSubcriptionPlanService.AdminViewSubscriptionPlanDetailAsync"/>.
/// </summary>
public class AdminViewSubscriptionPlanDetailAsyncTests
{
    private readonly Mock<IAdminSubcriptionPlanRepository> _mockRepo;
    private readonly AdminSubcriptionPlanService _sut;

    public AdminViewSubscriptionPlanDetailAsyncTests()
    {
        _mockRepo = new Mock<IAdminSubcriptionPlanRepository>();
        _sut = new AdminSubcriptionPlanService(_mockRepo.Object);
    }

    private static string FeaturesJson(string code, string targetRole) =>
        JsonSerializer.Serialize(new SubscriptionPlanMetadata
        {
            Code = code,
            TargetRole = targetRole,
            Features = new List<string> { "F1" },
            Benefits = new SubscriptionBenefitResponse
            {
                MonthlyJobPostLimit = 1,
                MonthlyApplicationLimit = 0,
                FeaturedBadge = false,
                SearchPriority = false,
                ListingDurationDays = 30,
                CanUseRecommendation = false
            }
        });

    // Condition: không có gói.
    [Fact]
    public async Task NotFound_ReturnsNull()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.FindAdminPlanByIdIncludingDeletedAsync(id)).ReturnsAsync((SubscriptionPlan?)null);

        var r = await _sut.AdminViewSubscriptionPlanDetailAsync(id);

        Assert.Null(r);
        _mockRepo.Verify(
            r => r.CountActiveSubscriptionsByPlanAsync(It.IsAny<Guid>(), It.IsAny<DateTime>()),
            Times.Never);
    }

    // Condition: tìm thấy — map từ MapPlan + số người đăng ký còn hiệu lực.
    [Fact]
    public async Task Found_ReturnsDetail()
    {
        var id = Guid.NewGuid();
        var plan = new SubscriptionPlan
        {
            Id = id,
            Name = "Pro Test",
            Description = "D",
            Features = FeaturesJson("P_TEST", "Parent"),
            Price = 50_000m,
            DurationDays = 60,
            SortOrder = 2,
            IsActive = true,
            CanUseRecommendation = true,
            IsDeleted = false,
            CreatedAt = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        _mockRepo.Setup(r => r.FindAdminPlanByIdIncludingDeletedAsync(id)).ReturnsAsync(plan);
        _mockRepo.Setup(r => r.CountActiveSubscriptionsByPlanAsync(id, It.IsAny<DateTime>()))
            .ReturnsAsync(4);

        var r = await _sut.AdminViewSubscriptionPlanDetailAsync(id);

        Assert.NotNull(r);
        Assert.Equal(id, r!.Id);
        Assert.Equal("Pro Test", r.Name);
        Assert.Equal("D", r.Description);
        Assert.Equal(50_000m, r.Price);
        Assert.Equal(60, r.DurationDays);
        Assert.Equal(2, r.SortOrder);
        Assert.Equal("P_TEST", r.Code);
        Assert.Equal("Parent", r.TargetRole);
        Assert.True(r.IsActive);
        Assert.Equal(4, r.ActiveSubscriberCount);
        Assert.True(r.CanUseRecommendation);
        Assert.Equal(plan.CreatedAt, r.CreatedAt);
        Assert.Equal(plan.UpdatedAt, r.UpdatedAt);
        Assert.NotEmpty(r.Features);
        _mockRepo.Verify(
            x => x.CountActiveSubscriptionsByPlanAsync(id, It.IsAny<DateTime>()),
            Times.Once);
    }
}
