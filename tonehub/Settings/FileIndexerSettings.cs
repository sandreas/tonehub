namespace tonehub.Settings;

public class FileIndexerSettings
{
    public TimeSpan DeleteOrphansAfter { get; set; } = new(0);
}