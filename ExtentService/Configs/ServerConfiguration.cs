namespace ExtentService.Configs;

public class ServerConfiguration
{
    public string? ServerPort { get; init; }
    public string? ServerHost { get; init; }
    public string? ServerRootPath { get; set; }
}