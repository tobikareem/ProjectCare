namespace CarePath.Application.Common.Exceptions;

public sealed class ResourceConflictException : Exception
{
    public ResourceConflictException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
