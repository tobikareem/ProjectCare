namespace CarePath.Application.Common.Exceptions;

public sealed class ResourceNotFoundException : Exception
{
    public ResourceNotFoundException(bool isPhiResource = true)
        : base("Resource not found.")
    {
        IsPhiResource = isPhiResource;
    }

    public bool IsPhiResource { get; }
}