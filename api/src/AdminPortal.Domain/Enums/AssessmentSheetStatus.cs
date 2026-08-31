namespace AdminPortal.Domain.Enums;

public enum AssessmentSheetStatus
{
    Open,
    Planed,
    Done,
    // Nhãn phân loại "đánh giá này không làm nữa". Không có side-effect nghiệp vụ:
    // vẫn cho chỉnh sửa như Open, chuyển qua/lại mọi trạng thái tự do, không đặt DoneDate.
    Canceled
}
