namespace Nanny_BackEnd.Enums;

public enum TransactionStatus
{
    Pending       = 1,  // Vừa tạo, chờ người dùng thanh toán
    Completed     = 2,  // Thanh toán thành công, subscription đã kích hoạt
    Failed        = 3,  // Hết hạn hoặc lỗi thanh toán
    WaitingReview = 5   // Người dùng bấm "Tôi đã chuyển khoản", chờ Casso đối soát
}
