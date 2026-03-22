namespace ApiServer.Models;

public abstract class BaseEntity
{
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public uint Version { get; set; }
}