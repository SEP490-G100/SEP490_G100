namespace WebSite.Models.Admin;

public class AdminDashboardDto
{
    public int     TotalUsers        { get; set; }
    public int     TotalParents      { get; set; }
    public int     TotalNannies      { get; set; }
    public int     TotalModerators   { get; set; }
    public decimal TotalRevenue      { get; set; }
    public int     TotalTransactions { get; set; }
    public List<RecentTransactionDto> RecentTransactions { get; set; } = new();
}

public class RecentTransactionDto
{
    public Guid     Id        { get; set; }
    public decimal  Amount    { get; set; }
    public int      Status    { get; set; }
    public int      Type      { get; set; }
    public string?  Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public string?  UserName  { get; set; }
    public string?  UserEmail { get; set; }

    public string StatusLabel => Status switch { 1 => "Completed", 0 => "Pending", _ => "Failed" };
    public string TypeLabel   => Type   switch { 1 => "Subscription", 2 => "Refund", _ => "Payment" };
}

public class CreateModeratorRequest
{
    public string  Email       { get; set; } = "";
    public string  Password    { get; set; } = "";
    public string  FirstName   { get; set; } = "";
    public string  LastName    { get; set; } = "";
    public string? PhoneNumber { get; set; }
}

public class UpdateModeratorRequest
{
    public string  FirstName   { get; set; } = "";
    public string  LastName    { get; set; } = "";
    public string? PhoneNumber { get; set; }
    public int     Status      { get; set; }
}
