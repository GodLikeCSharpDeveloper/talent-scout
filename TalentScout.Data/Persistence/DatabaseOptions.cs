using Npgsql;

namespace TalentScout.Data.Persistence;

public sealed class DatabaseOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5432;
    public string Name { get; set; } = "talentscout";
    public string Username { get; set; } = "talentscout";
    public string Password { get; set; } = "";

    public string ToConnectionString()
    {
        return new NpgsqlConnectionStringBuilder
        {
            Host = Host,
            Port = Port,
            Database = Name,
            Username = Username,
            Password = Password
        }.ConnectionString;
    }
}