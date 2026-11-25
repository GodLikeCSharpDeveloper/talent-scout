namespace TalentScout.Api.Shared;

public sealed class ApiException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;

    public static ApiException NotFound(string name)
    {
        return new ApiException(404, $"{name} was not found.");
    }

    public static ApiException Conflict(string message)
    {
        return new ApiException(409, message);
    }
}