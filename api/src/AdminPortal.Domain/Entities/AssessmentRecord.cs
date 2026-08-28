using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AdminPortal.Domain.Enums;

namespace AdminPortal.Domain.Entities;


public sealed class AssessmentRecord
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    [ForeignKey(nameof(AssessmentSheetId))]
    public required Guid AssessmentSheetId {get;set;}
    public required AssessmentSheet AssessmentSheet {get;set;}
    public int? AssessmentRowIndex {get;set;}

    /// <summary>
    /// STT hiển thị do người dùng chỉnh và lưu trên form bảng đánh giá. Tách biệt với <see cref="AssessmentRowIndex"/>
    /// (khóa sắp xếp gốc theo mục đánh giá). Null nghĩa là chưa đặt; UI tự đếm tăng dần theo nhóm lv3.
    /// </summary>
    public int? DisplayOrder {get;set;}

    [Column(TypeName = "jsonb")]
    public required AssessmentSnapshot AssessmentSnapshot { get; set; }
    public AssessmentGrade? PlanGrade { get; set; }
    public AssessmentGrade? FinalGrade { get; set; }
    [StringLength(2000)]
    public string? PlanNote { get; set; }
    [StringLength(2000)]
    public string? FinalNote { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    [ForeignKey(nameof(UpdatedByUserId))]
    public User? UpdatedByUser { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
