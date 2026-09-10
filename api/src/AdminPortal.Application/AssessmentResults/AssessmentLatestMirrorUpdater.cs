using AdminPortal.Application.Common;
using AdminPortal.Application.Common.Exceptions;
using AdminPortal.Application.Common.Interfaces;
using AdminPortal.Application.Common.Models;
using AdminPortal.Domain.Entities;
using AdminPortal.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AdminPortal.Application.AssessmentResults;

public sealed class AssessmentLatestMirrorUpdater(
    IApplicationDbContext dbContext,
    TimeProvider timeProvider) : IAssessmentLatestMirrorUpdater
{
    public async Task ApplyAsync(
        Guid studentId,
        IReadOnlyList<AssessmentLatestMirrorValue> values,
        ActorContext actor,
        CancellationToken cancellationToken)
    {
        if (values.Select(x => x.AssessmentId).Distinct().Count() != values.Count)
            throw new InvalidOperationException("Latest mirror updates must have unique assessment ids.");

        var student = await dbContext.Students.SingleOrDefaultAsync(x => x.Id == studentId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy học sinh.", ProblemCodes.StudentNotFound);
        var now = timeProvider.GetUtcNow();
        var latest = await dbContext.AssessmentSheetLatests
            .SingleOrDefaultAsync(x => x.StudentId == studentId, cancellationToken);
        var latestCreated = latest is null;
        if (latest is null)
        {
            latest = new AssessmentSheetLatest
            {
                Id = Guid.NewGuid(),
                Name = "Kết quả gần nhất",
                AssessmentSheetStatus = AssessmentSheetStatus.Open,
                StudentId = student.Id,
                Student = student,
                StudentSnapshot = BuildStudentSnapshot(student),
                UpdatedByUserId = actor.UserId,
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.AssessmentSheetLatests.Add(latest);
        }

        var assessmentIds = values.Select(x => x.AssessmentId).ToArray();
        var existing = await dbContext.AssessmentRecordLatests
            .Where(x => x.AssessmentSheetLatestId == latest.Id && assessmentIds.Contains(x.AssessmentId))
            .ToDictionaryAsync(x => x.AssessmentId, cancellationToken);
        var changed = latestCreated;

        foreach (var value in values)
        {
            var note = AssessmentResultRules.NormalizeNote(value.Note);
            if (value.Grade is null && note is null)
            {
                if (existing.TryGetValue(value.AssessmentId, out var obsolete))
                {
                    dbContext.AssessmentRecordLatests.Remove(obsolete);
                    changed = true;
                }
                continue;
            }

            if (!existing.TryGetValue(value.AssessmentId, out var record))
            {
                record = new AssessmentRecordLatest
                {
                    Id = Guid.NewGuid(),
                    AssessmentSheetLatestId = latest.Id,
                    AssessmentSheetLatest = latest,
                    AssessmentId = value.AssessmentId,
                    Assessment = null!,
                    LatestGrade = value.Grade,
                    Note = note,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                dbContext.AssessmentRecordLatests.Add(record);
                changed = true;
                continue;
            }

            if (record.LatestGrade == value.Grade && string.Equals(record.Note, note, StringComparison.Ordinal))
                continue;

            record.LatestGrade = value.Grade;
            record.Note = note;
            record.UpdatedAt = now;
            changed = true;
        }

        if (changed || !MatchesStudent(latest.StudentSnapshot, student))
        {
            latest.StudentSnapshot = BuildStudentSnapshot(student);
            latest.UpdatedByUserId = actor.UserId;
            latest.UpdatedAt = now;
        }
    }

    private static StudentSnapshot BuildStudentSnapshot(Student student) => new()
    {
        StudentCode = student.StudentCode,
        FullName = student.FullName,
        NickName = student.NickName,
        DateOfBirth = student.DateOfBirth,
        Gender = student.Gender
    };

    private static bool MatchesStudent(StudentSnapshot snapshot, Student student) =>
        string.Equals(snapshot.StudentCode, student.StudentCode, StringComparison.Ordinal) &&
        string.Equals(snapshot.FullName, student.FullName, StringComparison.Ordinal) &&
        string.Equals(snapshot.NickName, student.NickName, StringComparison.Ordinal) &&
        snapshot.DateOfBirth == student.DateOfBirth &&
        snapshot.Gender == student.Gender;
}
