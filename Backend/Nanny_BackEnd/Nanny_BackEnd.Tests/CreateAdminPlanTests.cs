using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Nanny_BackEnd.DTOs.Subscription;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// Tests for:
/// SubscriptionController.AdminCreateSubscriptionPlan -> SubscriptionService.createAdminPlan
/// </summary>
public class CreateAdminPlanTests
{
    private readonly Mock<ISubscriptionRepository> _mockRepo;
    private readonly SubscriptionService _sut;

    public CreateAdminPlanTests()
    {
        _mockRepo = new Mock<ISubscriptionRepository>();

        var mockNotificationService = new Mock<INotificationService>();
        var mockCassoService = new Mock<ICassoService>();
        var mockPayOsService = new Mock<IPayOsService>();
        var mockUserRepository = new Mock<IUserRepository>();
        var payOsOptions = Options.Create(new PayOsOptions { ExpiresAfterMinutes = 15 });

        _sut = new SubscriptionService(
            _mockRepo.Object,
            mockUserRepository.Object,
            mockNotificationService.Object,
            mockCassoService.Object,
            mockPayOsService.Object,
            payOsOptions,
            NullLogger<SubscriptionService>.Instance);
    }

    private static AdminSubscriptionPlanUpsertRequest ValidRequest() => new()
    {
        Name = "Goi Premium",
        Description = "Mo ta goi",
        TargetRole = "Parent",
        Price = 50_000m,
        DurationDays = 30,
        SortOrder = 99,
        Features = new List<string> { "Tinh nang A", "Tinh nang B" },
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

    // Condition: feature list only has whitespace -> service rejects before checking duplicate name.
    [Fact]
    public async Task Features_WhitespaceOnly_Throws_AndDoesNotCheckDuplicate()
    {
        var req = ValidRequest();
        req.Features = new List<string> { "   " };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.createAdminPlan(Guid.NewGuid(), req));

        Assert.Contains("feature", ex.Message, StringComparison.OrdinalIgnoreCase);
        _mockRepo.Verify(r => r.existsPlanNameIncludingDeleted(It.IsAny<string>(), It.IsAny<Guid?>()), Times.Never);
        _mockRepo.Verify(r => r.addPlan(It.IsAny<SubscriptionPlan>()), Times.Never);
    }

    // Condition: create success -> trim input, use next sort order, persist, then return detail.
    [Fact]
    public async Task Create_Success_AddsPlan_AndReturnsDetail()
    {
        var adminId = Guid.NewGuid();
        var req = ValidRequest();
        req.Name = "  Gói Mới  ";
        req.Description = "  Mô tả mới  ";
        req.Features = new List<string> { "  Parent benefit  ", "Parent benefit", "Extra" };
        req.SortOrder = 123; // service ignores this on create and always uses nextSortOrder.

        SubscriptionPlan? added = null;

        _mockRepo.Setup(r => r.getNextSubscriptionPlanSortOrder()).ReturnsAsync(4);
        _mockRepo.Setup(r => r.addPlan(It.IsAny<SubscriptionPlan>()))
            .Callback<SubscriptionPlan>(plan => added = plan);
        _mockRepo.Setup(r => r.saveChanges()).Returns(Task.CompletedTask);
        _mockRepo.Setup(r => r.findAdminPlanByIdIncludingDeleted(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => added != null && added.Id == id ? added : null);
        _mockRepo.Setup(r => r.countActiveSubscriptionsByPlan(It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .ReturnsAsync(2);

        var result = await _sut.createAdminPlan(adminId, req);

        Assert.NotNull(added);
        Assert.Equal("Gói Mới", added!.Name);
        Assert.Equal("Mô tả mới", added.Description);
        Assert.Equal(4, added.SortOrder);
        Assert.Equal(50_000m, added.Price);
        Assert.Equal(30, added.DurationDays);
        Assert.True(added.IsActive);
        Assert.False(added.IsDeleted);
        Assert.True(added.CanUseRecommendation);
        Assert.Equal(adminId, added.CreatedBy);
        Assert.NotEqual(Guid.Empty, added.Id);

        // Features are stored as structured metadata JSON (code, targetRole, features, benefits, isTrial).
        var meta = SubscriptionPlanMetadataHelper.TryParse(added.Features);
        Assert.NotNull(meta);
        Assert.Equal(2, meta!.Features.Count);
        Assert.Contains("Parent benefit", meta.Features);
        Assert.Contains("Extra", meta.Features);
        Assert.False(meta.IsTrial);

        Assert.Equal(added.Id, result.Id);
        Assert.Equal("Gói Mới", result.Name);
        Assert.Equal("Mô tả mới", result.Description);
        Assert.Equal(4, result.SortOrder);
        Assert.Equal(2, result.ActiveSubscriberCount);
        Assert.True(result.CanUseRecommendation);

        _mockRepo.Verify(r => r.getNextSubscriptionPlanSortOrder(), Times.Once);
        _mockRepo.Verify(r => r.addPlan(It.IsAny<SubscriptionPlan>()), Times.Once);
        _mockRepo.Verify(r => r.saveChanges(), Times.Once);
        _mockRepo.Verify(r => r.findAdminPlanByIdIncludingDeleted(added.Id), Times.Once);
        _mockRepo.Verify(r => r.countActiveSubscriptionsByPlan(added.Id, It.IsAny<DateTime>()), Times.Once);
    }

    // Condition: description has only whitespace -> stored as null and returned as null.
    [Fact]
    public async Task Create_EmptyDescription_StoresNull()
    {
        var req = ValidRequest();
        req.Description = "   ";

        SubscriptionPlan? added = null;
        _mockRepo.Setup(r => r.getNextSubscriptionPlanSortOrder()).ReturnsAsync(1);
        _mockRepo.Setup(r => r.addPlan(It.IsAny<SubscriptionPlan>()))
            .Callback<SubscriptionPlan>(plan => added = plan);
        _mockRepo.Setup(r => r.saveChanges()).Returns(Task.CompletedTask);
        _mockRepo.Setup(r => r.findAdminPlanByIdIncludingDeleted(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => added != null && added.Id == id ? added : null);
        _mockRepo.Setup(r => r.countActiveSubscriptionsByPlan(It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .ReturnsAsync(0);

        var result = await _sut.createAdminPlan(Guid.NewGuid(), req);

        Assert.NotNull(added);
        Assert.Null(added!.Description);
        Assert.Null(result.Description);
    }

    // Condition: create persisted but detail fetch returns null -> throw invalid operation.
    [Fact]
    public async Task Create_WhenDetailCannotBeLoaded_Throws()
    {
        _mockRepo.Setup(r => r.getNextSubscriptionPlanSortOrder()).ReturnsAsync(1);
        _mockRepo.Setup(r => r.addPlan(It.IsAny<SubscriptionPlan>()));
        _mockRepo.Setup(r => r.saveChanges()).Returns(Task.CompletedTask);
        _mockRepo.Setup(r => r.findAdminPlanByIdIncludingDeleted(It.IsAny<Guid>()))
            .ReturnsAsync((SubscriptionPlan?)null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.createAdminPlan(Guid.NewGuid(), ValidRequest()));

        Assert.Contains("subscription", ex.Message, StringComparison.OrdinalIgnoreCase);
        _mockRepo.Verify(r => r.addPlan(It.IsAny<SubscriptionPlan>()), Times.Once);
        _mockRepo.Verify(r => r.saveChanges(), Times.Once);
    }
}
