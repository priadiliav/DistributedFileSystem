namespace LockService.Configs;

public class LockState
{
    public required string OwnerId { get; init; }
    public long Sequence { get; set; }
}