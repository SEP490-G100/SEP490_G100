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
<<<<<<< Huy
    public const int HiringCancelled = 20;
    public const int ContractCreated = 21;
=======
    public const int JobPostingExpired = 20;
>>>>>>> develop
    public const int ModeratorBroadcast = 99;

    public static string getLabel(int type) => type switch
    {
        SubscriptionReminder => "Nhắc gia hạn gói",
        JobApplicationReceived => "Có người ứng tuyển",
        JobApplicationApproved => "Ứng tuyển được chấp nhận",
        JobApplicationRejected => "Ứng tuyển bị từ chối",
        AdminBroadcast => "Thông báo từ admin",
        SubscriptionPurchased => "Đăng ký gói thành công",
        JobPostingApproved => "Bài đăng đã được duyệt",
        JobPostingRejected => "Bài đăng bị từ chối",
        JobPostingPending => "Bài đăng đang chờ duyệt",
        NannyProfileFavorited => "Hồ sơ được yêu thích",
        VerificationRequestSubmitted => "Yêu cầu xác minh mới",
        ReportSubmitted => "Báo cáo mới",
        MessageToModerator => "Tin nhắn tới moderator",
        JobPostingReviewRequired => "Bài đăng mới cần duyệt",
        VerificationRequestApproved => "Yêu cầu xác minh được chấp thuận",
        VerificationRequestRejected => "Yêu cầu xác minh bị từ chối",
        VerificationRequestCreated => "Đã gửi yêu cầu xác minh",
        JobApplicationSubmitted => "Đơn ứng tuyển đã gửi",
        ContactRequestReceived => "Nhận yêu cầu liên hệ",
        ContactRequestAccepted => "Yêu cầu liên hệ được chấp nhận",
        ContactRequestRejected => "Yêu cầu liên hệ bị từ chối",
        HiringOffer => "Đề nghị việc làm",
        HiringAccepted => "Đề nghị việc làm đã được chấp nhận",
        HiringDeclined => "Đề nghị việc làm bị từ chối",
        HiringCompleted => "Hợp đồng đã hoàn thành",
        HiringConfirmed => "Xác nhận thuê bảo mẫu",
<<<<<<< Huy
        HiringCancelled => "Đề nghị thuê đã bị hủy",
        ContractCreated => "Hợp đồng đã được tạo",
=======
        JobPostingExpired => "Tin đăng hết hạn hiển thị",
>>>>>>> develop
        ModeratorBroadcast => "Thông báo từ Moderator",
        _ => "Thông báo hệ thống"
    };
}
