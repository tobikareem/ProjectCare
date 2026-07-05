namespace CarePath.Application.Common.Exceptions;

public sealed class ResourceAccessDeniedException : Exception
{
    public ResourceAccessDeniedException(string reasonCode, bool isPhiResource = true)
        : base("Resource access denied.")
    {
        ReasonCode = reasonCode;
        IsPhiResource = isPhiResource;
    }

    public string ReasonCode { get; }

    public bool IsPhiResource { get; }
}