namespace AdminPortal.Application.AssessmentResults;

public interface IAssessmentResultsService
{
    Task<AssessmentResultsResponse> GetAsync(Guid studentId, CancellationToken cancellationToken);
    Task<AssessmentResultsResponse> UpdateAsync(
        Guid studentId,
        UpdateAssessmentResultsRequest request,
        CancellationToken cancellationToken);
}
