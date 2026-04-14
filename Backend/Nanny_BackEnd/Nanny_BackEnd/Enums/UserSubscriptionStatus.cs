namespace Nanny_BackEnd.Enums;

public enum UserSubscriptionStatus
{
    Inactive  = 0,  // Hết hạn tự nhiên (do background expire)
    Active    = 1,  // Đang hoạt động
    Cancelled = 2   // Người dùng chủ động hủy
}
