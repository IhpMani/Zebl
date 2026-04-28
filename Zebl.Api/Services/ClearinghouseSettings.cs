namespace Zebl.Api.Services;

public sealed class ClearinghouseSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string RemotePath { get; set; } = "/";
    public string IncomingPath { get; set; } = "/";
    public string ProcessedPath { get; set; } = "/";
    public int EligibilityPollingMinutes { get; set; } = 2;
}
