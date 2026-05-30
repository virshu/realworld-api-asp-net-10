namespace RealWorld.Exceptions;

public class ConduitValidationException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}