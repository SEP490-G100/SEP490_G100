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
    public const int VerificationRequestSubmitted = 98;
    public const int ReportSubmitted = 92;
    public const int MessageToModerator = 93;
    public const int JobPostingReviewRequired = 94;
    public const int VerificationRequestApproved = 95;
    public const int VerificationRequestRejected = 96;
    public const int VerificationRequestCreated = 97;
    public const int JobApplicationSubmitted = 11;
    public const int ContactRequestReceived = 12;
    public const int ContactRequestAccepted = 13;
    public const int ContactRequestRejected = 14;
    public const int HiringOffer = 15;
    public const int HiringAccepted = 16;
    public const int HiringDeclined = 17;
    public const int HiringCompleted = 18;
    public const int HiringConfirmed = 19;
    public const int ModeratorBroadcast = 99;

    public static string getLabel(int type) => type switch
    {
        SubscriptionReminder => "Nhac gia han goi",
        JobApplicationReceived => "Có người ứng tuyển",
        JobApplicationApproved => "Ứng tuyển được chấp nhận",
        JobApplicationRejected => "Ứng tuyển bị từ chối",
        AdminBroadcast => "Thong bao tu admin",
        SubscriptionPurchased => "Đăng ký gói thành công",
        JobPostingApproved => "Bài đăng đã được duyệt",
        JobPostingRejected => "Bài đăng bị từ chối",
        JobPostingPending => "Bài đăng đang chờ duyệt",
        NannyProfileFavorited => "Hồ sơ được yêu thích",
        VerificationRequestSubmitted => "Yeu cau xac minh moi",
        ReportSubmitted => "Bao cao moi",
        MessageToModerator => "Tin nhan toi moderator",
        JobPostingReviewRequired => "Bài đăng mới cần duyệt",
        VerificationRequestApproved => "Yeu cau xac minh duoc chap thuan",
        VerificationRequestRejected => "Yeu cau xac minh bi tu choi",
        VerificationRequestCreated => "Da gui yeu cau xac minh",
        JobApplicationSubmitted => "Đơn ứng tuyển đã gửi",
        ContactRequestReceived => "Nhan request contact",
        ContactRequestAccepted => "Request contact duoc chap nhan",
        ContactRequestRejected => "Request contact bi tu choi",
        HiringOffer => "Đề nghị việc làm",
        HiringAccepted => "Đề nghị việc làm đã được chấp nhận",
        HiringDeclined => "Đề nghị việc làm bị từ chối",
        HiringCompleted => "Hop dong da hoan thanh",
        HiringConfirmed => "Xac nhan thue bao mau",
        ModeratorBroadcast => "Thong bao tu Moderator",
        _ => "Thong bao he thong"
    };
}
