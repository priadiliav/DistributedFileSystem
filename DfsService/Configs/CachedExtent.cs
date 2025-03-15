namespace DfsService.Configs;

public class CachedExtent
{
    public Google.Protobuf.ByteString FileData { get; set; }
    public bool IsDirty { get; set; }
    public bool IsRemoved { get; set; }
}