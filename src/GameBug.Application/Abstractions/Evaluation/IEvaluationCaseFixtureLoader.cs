using GameBug.Application.Evaluation;

namespace GameBug.Application.Abstractions.Evaluation;

public interface IEvaluationCaseFixtureLoader
{
    Task<EvaluationCaseFixture?> LoadAsync(string caseId, CancellationToken cancellationToken);
}
