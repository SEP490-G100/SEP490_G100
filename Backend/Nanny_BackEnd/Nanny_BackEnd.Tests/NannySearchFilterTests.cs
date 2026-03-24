using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.DTOs.Nanny;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Services;
using Xunit;

namespace Nanny_BackEnd.Tests;

public class NannySearchFilterTests
{
    [Fact]
    public async Task CombinedDayAndTimeFilter_MatchesSameSlotOnly()
    {
        await using var fixture = await TestFixture.Create();

        var result = await fixture.Service.GetListAsync(new NannyListRequest
        {
            DayOfWeek = 0,
            TimeSlot = 2,
            Page = 1,
            PageSize = 20
        });

        Assert.Single(result.Items);
        Assert.Equal("Binh Tran", result.Items[0].FullName);
    }

    [Fact]
    public async Task SkillFilter_ReturnsNanniesWithSelectedSkill()
    {
        await using var fixture = await TestFixture.Create();

        var result = await fixture.Service.GetListAsync(new NannyListRequest
        {
            SkillIds = fixture.FirstAidSkillId.ToString(),
            Page = 1,
            PageSize = 20
        });

        var names = result.Items.Select(i => i.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Equal(2, result.TotalCount);
        Assert.Contains("Binh Tran", names);
        Assert.Contains("Cuc Le", names);
    }

    [Fact]
    public async Task CityAndDistrictFilter_AppliesTogether()
    {
        await using var fixture = await TestFixture.Create();

        var result = await fixture.Service.GetListAsync(new NannyListRequest
        {
            City = "ha noi",
            District = "dong da",
            Page = 1,
            PageSize = 20
        });

        Assert.Single(result.Items);
        Assert.Equal("Anna Nguyen", result.Items[0].FullName);
    }

    [Fact]
    public async Task AgeRangeFilter_ReturnsOnlyProfilesWithinBounds()
    {
        await using var fixture = await TestFixture.Create();

        var result = await fixture.Service.GetListAsync(new NannyListRequest
        {
            MinAge = 30,
            MaxAge = 40,
            Page = 1,
            PageSize = 20
        });

        Assert.Single(result.Items);
        Assert.Equal("Binh Tran", result.Items[0].FullName);
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        private TestFixture(Sep490NannyDbContext db, NannyService service)
        {
            Db = db;
            Service = service;
        }

        public Sep490NannyDbContext Db { get; }
        public NannyService Service { get; }
        public Guid FirstAidSkillId { get; private set; }

        public static async Task<TestFixture> Create()
        {
            var options = new DbContextOptionsBuilder<Sep490NannyDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var db = new Sep490NannyDbContext(options);
            var repo = new NannyProfileRepository(db);
            var service = new NannyService(repo);
            var fixture = new TestFixture(db, service);
            await fixture.Seed();
            return fixture;
        }

        private async Task Seed()
        {
            var now = DateTime.UtcNow;

            var cookingSkillId = Guid.NewGuid();
            FirstAidSkillId = Guid.NewGuid();

            Db.Skills.AddRange(
                new Skill
                {
                    Id = cookingSkillId,
                    Name = "Cooking",
                    Category = "Home",
                    SortOrder = 1,
                    IsActive = true,
                    IsDeleted = false,
                    CreatedAt = now
                },
                new Skill
                {
                    Id = FirstAidSkillId,
                    Name = "First Aid",
                    Category = "Health",
                    SortOrder = 2,
                    IsActive = true,
                    IsDeleted = false,
                    CreatedAt = now
                }
            );

            await AddNanny(
                firstName: "Anna",
                lastName: "Nguyen",
                city: "Ha Noi",
                district: "Dong Da",
                dateOfBirth: new DateOnly(1998, 1, 10),
                experience: 5,
                verificationStatus: 2,
                salaryMin: 8000000m,
                salaryMax: 12000000m,
                latitude: 21.0278m,
                longitude: 105.8342m,
                availability: [(0, 0), (1, 2)],
                skillIds: [cookingSkillId]);

            await AddNanny(
                firstName: "Binh",
                lastName: "Tran",
                city: "Ha Noi",
                district: "Cau Giay",
                dateOfBirth: new DateOnly(1992, 5, 15),
                experience: 7,
                verificationStatus: 1,
                salaryMin: 10000000m,
                salaryMax: 15000000m,
                latitude: 21.0368m,
                longitude: 105.7905m,
                availability: [(0, 2)],
                skillIds: [FirstAidSkillId]);

            await AddNanny(
                firstName: "Cuc",
                lastName: "Le",
                city: "Da Nang",
                district: "Hai Chau",
                dateOfBirth: new DateOnly(1980, 3, 20),
                experience: 10,
                verificationStatus: 2,
                salaryMin: 15000000m,
                salaryMax: 20000000m,
                latitude: 16.0544m,
                longitude: 108.2022m,
                availability: [(2, 1)],
                skillIds: [cookingSkillId, FirstAidSkillId]);

            await Db.SaveChangesAsync();
        }

        private async Task AddNanny(
            string firstName,
            string lastName,
            string city,
            string district,
            DateOnly dateOfBirth,
            int experience,
            int verificationStatus,
            decimal salaryMin,
            decimal salaryMax,
            decimal latitude,
            decimal longitude,
            IEnumerable<(int dayOfWeek, int timeSlot)> availability,
            IEnumerable<Guid> skillIds)
        {
            var now = DateTime.UtcNow;
            var userId = Guid.NewGuid();
            var profileId = Guid.NewGuid();

            Db.Users.Add(new User
            {
                Id = userId,
                Email = $"{firstName.ToLowerInvariant()}.{lastName.ToLowerInvariant()}@example.com",
                FirstName = firstName,
                LastName = lastName,
                City = city,
                District = district,
                DateOfBirth = dateOfBirth,
                Latitude = latitude,
                Longitude = longitude,
                Status = 1,
                AuthProvider = 0,
                EmailConfirmed = true,
                PhoneConfirmed = false,
                IsDeleted = false,
                CreatedAt = now
            });

            Db.NannyProfiles.Add(new NannyProfile
            {
                Id = profileId,
                UserId = userId,
                Bio = $"{firstName} has strong childcare skills.",
                YearsOfExperience = experience,
                VerificationStatus = verificationStatus,
                ExpectedSalaryMin = salaryMin,
                ExpectedSalaryMax = salaryMax,
                SalaryType = 1,
                TotalReviews = 0,
                ProfileCompleteness = 80,
                IsDeleted = false,
                CreatedAt = now
            });

            foreach (var (dayOfWeek, timeSlot) in availability)
            {
                Db.NannyAvailabilities.Add(new NannyAvailability
                {
                    Id = Guid.NewGuid(),
                    NannyProfileId = profileId,
                    DayOfWeek = dayOfWeek,
                    TimeSlot = timeSlot,
                    IsAvailable = true,
                    IsDeleted = false,
                    CreatedAt = now
                });
            }

            foreach (var skillId in skillIds)
            {
                Db.NannySkills.Add(new NannySkill
                {
                    Id = Guid.NewGuid(),
                    NannyProfileId = profileId,
                    SkillId = skillId,
                    IsDeleted = false,
                    CreatedAt = now
                });
            }

            await Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
