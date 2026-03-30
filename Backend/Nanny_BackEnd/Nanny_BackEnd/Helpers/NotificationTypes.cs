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
    public const int VerificationRequestSubmitted = 18;
    public const int ReportSubmitted = 12;
    public const int MessageToModerator = 13;
    public const int JobPostingReviewRequired = 14;
    public const int VerificationRequestApproved = 15;
    public const int VerificationRequestRejected = 16;
    public const int VerificationRequestCreated = 17;
    public const int JobApplicationSubmitted = 11;
    public const int ContactRequestReceived = 12;
    public const int ContactRequestAccepted = 13;
    public const int ContactRequestRejected = 14;

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
        VerificationRequestSubmitted => "Yeu cau xac minh moi",
        ReportSubmitted => "Bao cao moi",
        MessageToModerator => "Tin nhan toi moderator",
        JobPostingReviewRequired => "Bai dang moi can duyet",
        VerificationRequestApproved => "Yeu cau xac minh duoc chap thuan",
        VerificationRequestRejected => "Yeu cau xac minh bi tu choi",
        VerificationRequestCreated => "Da gui yeu cau xac minh",
        JobApplicationSubmitted => "Don ung tuyen da gui",
        ContactRequestReceived => "Nhan request contact",
        ContactRequestAccepted => "Request contact duoc chap nhan",
        ContactRequestRejected => "Request contact bi tu choi",
        _ => "Thong bao he thong"
    };
}
