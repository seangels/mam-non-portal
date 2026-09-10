using AdminPortal.Application.Common.Models;

namespace AdminPortal.Application.AssessmentResults;

public interface IAssessmentLatestMirrorUpdater
{
    Task ApplyAsync(
        Guid studentId,
        IReadOnlyList<AssessmentLatestMirrorValue> values,
        ActorContext actor,
        CancellationToken cancellationToken);
}
