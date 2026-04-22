using FluentAssertions;
using Moq;
using Nanny_BackEnd.DTOs.Subscription;
using Nanny_BackEnd.Models;
namespace Nanny_BackEnd.Tests;

public class RecommendationGatingTests
{
    [Fact]
    public async Task NanniesForJob_NoUser_401()
    {
        var b = RecommendationServiceTestBuilder.Create();
        var r = await b.Sut.ValidateNanniesForJobGatingAsync(null, Guid.NewGuid(), false);
        r.IsAllowed.Should().BeFalse();
        r.HttpStatus.Should().Be(401);
    }

    [Fact]
    public async Task NanniesForJob_Admin_SkipsParentAndJobChecks()
    {
        var b = RecommendationServiceTestBuilder.Create();
        var r = await b.Sut.ValidateNanniesForJobGatingAsync(Guid.NewGuid(), Guid.NewGuid(), isAdminOrModerator: true);
        r.IsAllowed.Should().BeTrue();
        b.Parent.Verify(p => p.FindByUserIdAsync(It.IsAny<Guid>()), Times.Never);
        b.Job.Verify(j => j.JobPostingExistsForParentAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task NanniesForJob_NoParent_403()
    {
        var b = RecommendationServiceTestBuilder.Create();
        b.Parent.Setup(p => p.FindByUserIdAsync(It.IsAny<Guid>())).ReturnsAsync((ParentProfile?)null);
        var r = await b.Sut.ValidateNanniesForJobGatingAsync(Guid.NewGuid(), Guid.NewGuid(), false);
        r.IsAllowed.Should().BeFalse();
        r.HttpStatus.Should().Be(403);
    }

    [Fact]
    public async Task NanniesForJob_RecommendationNotInPlan_402()
    {
        var b = RecommendationServiceTestBuilder.Create();
        var parent = new ParentProfile { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        b.Parent.Setup(p => p.FindByUserIdAsync(parent.UserId)).ReturnsAsync(parent);
        b.Sub.Setup(s => s.getBenefitsForParentProfile(parent.Id))
            .ReturnsAsync(SubscriptionBenefitResponse.FreeParent);

        var r = await b.Sut.ValidateNanniesForJobGatingAsync(parent.UserId, Guid.NewGuid(), false);
        r.IsAllowed.Should().BeFalse();
        r.HttpStatus.Should().Be(402);
        r.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task NanniesForJob_JobNotFoundForParent_404()
    {
        var b = RecommendationServiceTestBuilder.Create();
        var jobId = Guid.NewGuid();
        var parent = new ParentProfile { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        b.Parent.Setup(p => p.FindByUserIdAsync(parent.UserId)).ReturnsAsync(parent);
        b.Sub.Setup(s => s.getBenefitsForParentProfile(parent.Id))
            .ReturnsAsync(new SubscriptionBenefitResponse { CanUseRecommendation = true });
        b.Job.Setup(j => j.JobPostingExistsForParentAsync(jobId, parent.Id)).ReturnsAsync(false);

        var r = await b.Sut.ValidateNanniesForJobGatingAsync(parent.UserId, jobId, false);
        r.IsAllowed.Should().BeFalse();
        r.HttpStatus.Should().Be(404);
    }

    [Fact]
    public async Task NanniesForJob_Ok()
    {
        var b = RecommendationServiceTestBuilder.Create();
        var jobId = Guid.NewGuid();
        var parent = new ParentProfile { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        b.Parent.Setup(p => p.FindByUserIdAsync(parent.UserId)).ReturnsAsync(parent);
        b.Sub.Setup(s => s.getBenefitsForParentProfile(parent.Id))
            .ReturnsAsync(new SubscriptionBenefitResponse { CanUseRecommendation = true });
        b.Job.Setup(j => j.JobPostingExistsForParentAsync(jobId, parent.Id)).ReturnsAsync(true);

        var r = await b.Sut.ValidateNanniesForJobGatingAsync(parent.UserId, jobId, false);
        r.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task JobsForNanny_NoUser_401()
    {
        var b = RecommendationServiceTestBuilder.Create();
        var r = await b.Sut.ValidateJobsForNannyGatingAsync(null);
        r.IsAllowed.Should().BeFalse();
        r.HttpStatus.Should().Be(401);
    }

    [Fact]
    public async Task JobsForNanny_NoNanny_404()
    {
        var b = RecommendationServiceTestBuilder.Create();
        b.Nanny.Setup(n => n.FindByUserIdAsync(It.IsAny<Guid>())).ReturnsAsync((NannyProfile?)null);
        var r = await b.Sut.ValidateJobsForNannyGatingAsync(Guid.NewGuid());
        r.IsAllowed.Should().BeFalse();
        r.HttpStatus.Should().Be(404);
    }

    [Fact]
    public async Task JobsForNanny_RecommendationNotInPlan_402()
    {
        var b = RecommendationServiceTestBuilder.Create();
        var nanny = new NannyProfile { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        b.Nanny.Setup(n => n.FindByUserIdAsync(nanny.UserId)).ReturnsAsync(nanny);
        b.Sub.Setup(s => s.getBenefitsForNannyProfile(nanny.Id))
            .ReturnsAsync(SubscriptionBenefitResponse.FreeNanny);

        var r = await b.Sut.ValidateJobsForNannyGatingAsync(nanny.UserId);
        r.IsAllowed.Should().BeFalse();
        r.HttpStatus.Should().Be(402);
    }

    [Fact]
    public async Task JobsForNanny_Ok_ReturnsNannyId()
    {
        var b = RecommendationServiceTestBuilder.Create();
        var nanny = new NannyProfile { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        b.Nanny.Setup(n => n.FindByUserIdAsync(nanny.UserId)).ReturnsAsync(nanny);
        b.Sub.Setup(s => s.getBenefitsForNannyProfile(nanny.Id))
            .ReturnsAsync(new SubscriptionBenefitResponse { CanUseRecommendation = true });

        var r = await b.Sut.ValidateJobsForNannyGatingAsync(nanny.UserId);
        r.IsAllowed.Should().BeTrue();
        r.NannyProfileId.Should().Be(nanny.Id);
    }
}
