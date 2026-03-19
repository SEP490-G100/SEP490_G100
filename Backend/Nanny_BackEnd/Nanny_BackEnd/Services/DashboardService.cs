using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.DTOs.Dashboard;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Services;

public class DashboardService
{
    private readonly UserRepository _userRepo;
    private readonly TransactionRepository _transactionRepo;
    private readonly UserSubscriptionRepository _subscriptionRepo;
    private readonly VerificationRequestRepository _verificationRepo;
    private readonly JobRepository _jobRepo;
    private readonly ContractRepository _contractRepo;

    public DashboardService(
        UserRepository userRepo,
        TransactionRepository transactionRepo,
        UserSubscriptionRepository subscriptionRepo,
        VerificationRequestRepository verificationRepo,
        JobRepository jobRepo,
        ContractRepository contractRepo)
    {
        _userRepo         = userRepo;
        _transactionRepo  = transactionRepo;
        _subscriptionRepo = subscriptionRepo;
        _verificationRepo = verificationRepo;
        _jobRepo          = jobRepo;
        _contractRepo     = contractRepo;
    }

    public async Task<DashboardStatsDto> GetDashboardStatsAsync()
    {
        // User stats — via UserRepository
        var userStats = await GetUserStatsAsync();

        // Revenue stats — via TransactionRepository
        var revenueStats = await GetRevenueStatsAsync();

        // Subscription stats — via UserSubscriptionRepository
        var subscriptionStats = await GetSubscriptionStatsAsync();

        // Platform Health stats
        var pendingVerifications = await _verificationRepo.GetQuery()
                                           .CountAsync(v => !v.IsDeleted && v.Status == (int)VerificationStatus.Pending);
        var activeJobs = await _jobRepo.GetQuery().CountAsync(j => !j.IsDeleted && j.Status == 1); // 1 = Open
        var totalContracts = await _contractRepo.GetQuery()
                                           .CountAsync(c => !c.IsDeleted);

        var platformHealth = new PlatformHealthStatsDto 
        {
            PendingVerifications = pendingVerifications,
            ActiveJobPostings = activeJobs,
            TotalContracts = totalContracts
        };

        return new DashboardStatsDto
        {
            UserStats         = userStats,
            RevenueStats      = revenueStats,
            SubscriptionStats = subscriptionStats,
            PlatformHealth    = platformHealth
        };
    }

    private async Task<UserStatsDto> GetUserStatsAsync()
    {
        var totalUsers      = await _userRepo.GetTotalUsersCountAsync();
        var totalParents    = await _userRepo.GetUserCountByRoleAsync("Parent");
        var totalNannies    = await _userRepo.GetUserCountByRoleAsync("Nanny");
        var totalModerators = await _userRepo.GetUserCountByRoleAsync("Moderator");

        return new UserStatsDto
        {
            TotalUsers      = totalUsers,
            TotalParents    = totalParents,
            TotalNannies    = totalNannies,
            TotalModerators = totalModerators
        };
    }

    private async Task<RevenueStatsDto> GetRevenueStatsAsync()
    {
        var totalRevenue    = await _transactionRepo.GetTotalRevenueAsync();
        var recentTx        = await _transactionRepo.GetRecentTransactionsAsync(5);
        var monthlyRevenue  = await _transactionRepo.GetMonthlyRevenueAsync(12);

        return new RevenueStatsDto
        {
            TotalRevenue   = totalRevenue,
            MonthlyRevenue = monthlyRevenue.Select(m => new MonthlyRevenueDto
            {
                Year    = m.Year,
                Month   = m.Month,
                Revenue = m.Revenue
            }).ToList(),
            RecentTransactions = recentTx.Select(t => new RecentTransactionDto
            {
                Id          = t.Id,
                Amount      = t.Amount,
                Status      = t.Status,
                Type        = t.Type,
                Description = t.Description,
                CreatedAt   = t.CreatedAt,
                UserName    = t.User?.FirstName + " " + t.User?.LastName,
                UserEmail   = t.User?.Email
            }).ToList()
        };
    }

    private async Task<SubscriptionStatsDto> GetSubscriptionStatsAsync()
    {
        var total              = await _subscriptionRepo.GetTotalSubscriptionsAsync();
        var active             = await _subscriptionRepo.GetActiveSubscriptionsAsync();
        var expired            = await _subscriptionRepo.GetExpiredSubscriptionsAsync();
        var monthlySubscriptions = await _subscriptionRepo.GetMonthlySubscriptionsAsync(12);

        return new SubscriptionStatsDto
        {
            TotalSubscriptions  = total,
            ActiveSubs          = active,
            ExpiredSubs         = expired,
            MonthlySubs         = monthlySubscriptions.Select(m => new MonthlySubscriptionDto
            {
                Year  = m.Year,
                Month = m.Month,
                Count = m.Count
            }).ToList()
        };
    }
}
