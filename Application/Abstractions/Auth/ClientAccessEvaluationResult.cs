namespace CarePath.Application.Abstractions.Auth;

public sealed record ClientAccessEvaluationResult(
    bool IsAuthorized,
    string? DenialCode = null)
{
    public const string NoGrant = "NoGrant";
    public const string ScopeInsufficient = "ScopeInsufficient";

    public static ClientAccessEvaluationResult Authorized() => new(true);

    public static ClientAccessEvaluationResult Denied(string denialCode) => new(false, denialCode);
}
