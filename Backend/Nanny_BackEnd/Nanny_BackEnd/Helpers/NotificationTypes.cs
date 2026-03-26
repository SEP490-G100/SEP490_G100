namespace Nanny_BackEnd.Helpers;

public static class NotificationTypes
{
    public const int SubscriptionReminder = 1;
    public const int JobApplicationReceived = 2;
    public const int JobApplicationApproved = 3;
    public const int JobApplicationRejected = 4;
    public const int AdminBroadcast = 5;
    public const int SubscriptionPurchased = 6;
    public const int JobPostingApproved = 7;
    public const int JobPostingRejected = 8;
    public const int JobPostingPending = 9;
    public const int NannyProfileFavorited = 10;
    public const int JobApplicationSubmitted = 11;

    public static string getLabel(int type) => type switch
    {
        SubscriptionReminder => "Nhac gia han goi",
        JobApplicationReceived => "Co nguoi ung tuyen",
        JobApplicationApproved => "Ung tuyen duoc chap nhan",
        JobApplicationRejected => "Ung tuyen bi tu choi",
        AdminBroadcast => "Thong bao tu admin",
        SubscriptionPurchased => "Dang ky goi thanh cong",
        JobPostingApproved => "Bai dang da duoc duyet",
        JobPostingRejected => "Bai dang bi tu choi",
        JobPostingPending => "Bai dang dang cho duyet",
        NannyProfileFavorited => "Ho so duoc yeu thich",
        JobApplicationSubmitted => "Don ung tuyen da gui",
        _ => "Thong bao he thong"
    };
}
