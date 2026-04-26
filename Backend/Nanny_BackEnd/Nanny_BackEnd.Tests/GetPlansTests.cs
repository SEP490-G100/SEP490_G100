using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// </summary>
public class GetPlansTests
{
    private readonly Mock<ISubscriptionRepository> _mockRepo;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<INotificationService>     _mockNotif;
    private readonly Mock<ICassoService>          _mockCasso;
    private readonly Mock<IPayOsService>          _mockPayOs;
    private readonly SubscriptionService          _sut;

    public GetPlansTests()
    {
        _mockRepo  = new Mock<ISubscriptionRepository>();
        _mockUserRepo = new Mock<IUserRepository>();
        _mockNotif = new Mock<INotificationService>();
        _mockCasso = new Mock<ICassoService>();
        _mockPayOs = new Mock<IPayOsService>();
        var payOpt = Options.Create(new PayOsOptions { ExpiresAfterMinutes = 15 });
        _sut = new SubscriptionService(
            _mockRepo.Object,
            _mockUserRepo.Object,
            _mockNotif.Object,
            _mockCasso.Object,
            _mockPayOs.Object,
            payOpt,
            NullLogger<SubscriptionService>.Instance);
    }

    [Fact]
    public async Task NoPlans_ReturnsEmpty()
    {
        _mockRepo.Setup(r => r.getActivePlans()).ReturnsAsync(new List<SubscriptionPlan>());

        var list = await _sut.getPlans();

        Assert.Empty(list);
        _mockRepo.Verify(r => r.getActivePlans(), Times.Once);
    }

    [Fact]
    public async Task OnePlan_MapsNamePriceAndInferredTargetRole()
    {
        var id = Guid.NewGuid();
        var p = new SubscriptionPlan
        {
            Id            = id,
            Name          = "Gói phụ huynh premium",
            Description   = "Bai dang; gia dinh",
            Features      = "3 bai; parent",
            Price         = 500000m,
            DurationDays  = 60,
            SortOrder     = 1,
            IsActive      = true,
            IsDeleted     = false,
            CreatedAt     = DateTime.UtcNow
        };
        _mockRepo.Setup(r => r.getActivePlans()).ReturnsAsync(new List<SubscriptionPlan> { p });

        var list = await _sut.getPlans();
        var r = Assert.Single(list);

        Assert.Equal(id, r.Id);
        Assert.Equal("Gói phụ huynh premium", r.Name);
        Assert.Equal(500000m, r.Price);
        Assert.Equal(60, r.DurationDays);
        Assert.Equal(1, r.SortOrder);
        Assert.Equal("Parent", r.TargetRole);
        Assert.NotNull(r.Code);
        Assert.NotEmpty(r.Code);
    }
}
