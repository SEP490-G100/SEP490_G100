using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;

namespace Nanny_BackEnd.Services;

public class ExportService
{
    private readonly Sep490NannyDbContext _db;

    public ExportService(Sep490NannyDbContext db)
    {
        _db = db;
    }

    public async Task<byte[]> ExportSystemDataToExcelAsync()
    {
        using var workbook = new XLWorkbook();

        // ── Sheet 1: Summary ──────────────────────────────────────────
        var summarySheet = workbook.Worksheets.Add("Summary");
        summarySheet.Cell(1, 1).Value = "Metric";
        summarySheet.Cell(1, 2).Value = "Value";

        var summaryHeaderRow = summarySheet.Row(1);
        summaryHeaderRow.Style.Font.Bold = true;
        summaryHeaderRow.Style.Fill.BackgroundColor = XLColor.LightGray;

        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1);

        var totalRevenue = await _db.Transactions
            .Where(t => !t.IsDeleted && t.Status == 1) // 1 = Completed
            .SumAsync(t => (decimal?)t.Amount) ?? 0;

        var currentMonthRevenue = await _db.Transactions
            .Where(t => !t.IsDeleted && t.Status == 1 && t.CreatedAt >= startOfMonth)
            .SumAsync(t => (decimal?)t.Amount) ?? 0;

        var totalSubscriptions = await _db.UserSubscriptions.CountAsync(s => !s.IsDeleted);

        var currentMonthSubscriptions = await _db.UserSubscriptions
            .CountAsync(s => !s.IsDeleted && s.CreatedAt >= startOfMonth);

        summarySheet.Cell(2, 1).Value = "Total Revenue";
        summarySheet.Cell(2, 2).Value = totalRevenue;
        summarySheet.Cell(2, 2).Style.NumberFormat.Format = "#,##0";

        summarySheet.Cell(3, 1).Value = "Current Month Revenue";
        summarySheet.Cell(3, 2).Value = currentMonthRevenue;
        summarySheet.Cell(3, 2).Style.NumberFormat.Format = "#,##0";

        summarySheet.Cell(4, 1).Value = "Total Subscriptions";
        summarySheet.Cell(4, 2).Value = totalSubscriptions;

        summarySheet.Cell(5, 1).Value = "Current Month Subscriptions";
        summarySheet.Cell(5, 2).Value = currentMonthSubscriptions;

        summarySheet.Columns().AdjustToContents();

        // Sheet 2: Users
        var usersSheet = workbook.Worksheets.Add("Users");
        usersSheet.Cell(1, 1).Value = "ID";
        usersSheet.Cell(1, 2).Value = "FirstName";
        usersSheet.Cell(1, 3).Value = "LastName";
        usersSheet.Cell(1, 4).Value = "Email";
        usersSheet.Cell(1, 5).Value = "Phone";
        usersSheet.Cell(1, 6).Value = "Status";
        usersSheet.Cell(1, 7).Value = "Roles";
        usersSheet.Cell(1, 8).Value = "CreatedAt";
        
        var usersSheetHeaderRow = usersSheet.Row(1);
        usersSheetHeaderRow.Style.Font.Bold = true;
        usersSheetHeaderRow.Style.Fill.BackgroundColor = XLColor.LightGray;

        var users = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        int userRow = 2;
        foreach (var u in users)
        {
            usersSheet.Cell(userRow, 1).Value = u.Id.ToString();
            usersSheet.Cell(userRow, 2).Value = u.FirstName;
            usersSheet.Cell(userRow, 3).Value = u.LastName;
            usersSheet.Cell(userRow, 4).Value = u.Email;
            usersSheet.Cell(userRow, 5).Value = u.PhoneNumber;
            usersSheet.Cell(userRow, 6).Value = GetUserStatusString(u.Status);
            usersSheet.Cell(userRow, 7).Value = string.Join(", ", u.UserRoles.Select(r => r.Role.Name));
            usersSheet.Cell(userRow, 8).Value = u.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
            userRow++;
        }
        usersSheet.Columns().AdjustToContents();

        // Sheet 2: Transactions
        var txSheet = workbook.Worksheets.Add("Transactions");
        txSheet.Cell(1, 1).Value = "TxID";
        txSheet.Cell(1, 2).Value = "Amount";
        txSheet.Cell(1, 3).Value = "Status";
        txSheet.Cell(1, 4).Value = "Type";
        txSheet.Cell(1, 5).Value = "Description";
        txSheet.Cell(1, 6).Value = "UserEmail";
        txSheet.Cell(1, 7).Value = "CreatedAt";

        var txSheetHeaderRow = txSheet.Row(1);
        txSheetHeaderRow.Style.Font.Bold = true;
        txSheetHeaderRow.Style.Fill.BackgroundColor = XLColor.LightGray;

        var transactions = await _db.Transactions
            .Include(t => t.User)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        int txRow = 2;
        foreach (var t in transactions)
        {
            txSheet.Cell(txRow, 1).Value = t.Id.ToString();
            txSheet.Cell(txRow, 2).Value = t.Amount;
            txSheet.Cell(txRow, 3).Value = GetTxStatusString(t.Status);
            txSheet.Cell(txRow, 4).Value = GetTxTypeString(t.Type);
            txSheet.Cell(txRow, 5).Value = t.Description;
            txSheet.Cell(txRow, 6).Value = t.User?.Email;
            txSheet.Cell(txRow, 7).Value = t.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
            txRow++;
        }
        txSheet.Columns().AdjustToContents();

        // Sheet 3: Subscriptions
        var subSheet = workbook.Worksheets.Add("Subscriptions");
        subSheet.Cell(1, 1).Value = "SubID";
        subSheet.Cell(1, 2).Value = "UserEmail";
        subSheet.Cell(1, 3).Value = "PlanName";
        subSheet.Cell(1, 4).Value = "StartDate";
        subSheet.Cell(1, 5).Value = "EndDate";
        subSheet.Cell(1, 6).Value = "Status";

        var subSheetHeaderRow = subSheet.Row(1);
        subSheetHeaderRow.Style.Font.Bold = true;
        subSheetHeaderRow.Style.Fill.BackgroundColor = XLColor.LightGray;

        var subs = await _db.UserSubscriptions
            .Include(s => s.User)
            .Include(s => s.SubscriptionPlan)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        int subRow = 2;
        foreach (var s in subs)
        {
            subSheet.Cell(subRow, 1).Value = s.Id.ToString();
            subSheet.Cell(subRow, 2).Value = s.User?.Email;
            subSheet.Cell(subRow, 3).Value = s.SubscriptionPlan?.Name;
            subSheet.Cell(subRow, 4).Value = s.StartDate.ToString("yyyy-MM-dd");
            subSheet.Cell(subRow, 5).Value = s.EndDate.ToString("yyyy-MM-dd");
            subSheet.Cell(subRow, 6).Value = s.Status == 1 ? "Active" : "Expired/Inactive";
            subRow++;
        }
        subSheet.Columns().AdjustToContents();

        // Convert the workbook to byte array
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string GetUserStatusString(int status) => status switch
    {
        0 => "Pending",
        1 => "Active",
        2 => "Inactive",
        3 => "Banned",
        _ => "Unknown"
    };

    private static string GetTxStatusString(int status) => status switch
    {
        0 => "Pending",
        1 => "Completed",
        2 => "Failed",
        3 => "Refunded",
        _ => "Unknown"
    };

    private static string GetTxTypeString(int type) => type switch
    {
        1 => "Subscription",
        2 => "Refund",
        _ => "Payment"
    };
}
