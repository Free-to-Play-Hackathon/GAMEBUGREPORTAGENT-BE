namespace GameBug.Domain.Evaluation;

public enum EvaluationValidity
{
    ValidForClaim,
    InvalidForClaim
}

public enum InvalidReasonCode
{
    MissingManifestHash,
    MissingComponentVersion,
    ManifestHashMismatch,
    ConfigurationHashMismatch
}
