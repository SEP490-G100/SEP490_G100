namespace Nanny_BackEnd.DTOs.Dashboard;

public class DashboardModerationEventQueryDto
{
    public DateTime Date { get; set; }
    public int Status { get; set; }
}

public class DashboardRevenueEventQueryDto
{
    public DateTime Date { get; set; }
    public decimal Revenue { get; set; }
}

public class DashboardMonthlyRevenueQueryDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Revenue { get; set; }
}

public class DashboardRecentTransactionQueryDto
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public int Status { get; set; }
    public int Type { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? UserName { get; set; }
    public string? UserEmail { get; set; }
}

public class DashboardMonthlySubscriptionQueryDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int Count { get; set; }
}
