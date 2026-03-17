namespace Nanny_BackEnd.Helpers;

public static class NotificationTypes
{
    public const int SubscriptionReminder = 1;
    public const int JobApplicationReceived = 2;
    public const int JobApplicationApproved = 3;
    public const int JobApplicationRejected = 4;
    public const int AdminBroadcast = 5;
    public const int SubscriptionPurchased = 6;

    public static string getLabel(int type) => type switch
    {
        SubscriptionReminder => "Nhac gia han goi",
        JobApplicationReceived => "Co nguoi ung tuyen",
        JobApplicationApproved => "Ung tuyen duoc chap nhan",
        JobApplicationRejected => "Ung tuyen bi tu choi",
        AdminBroadcast => "Thong bao tu admin",
        SubscriptionPurchased => "Dang ky goi thanh cong",
        _ => "Thong bao he thong"
    };
}
