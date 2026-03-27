using System.ComponentModel.DataAnnotations;

namespace ApiServer.Models;

public abstract class BaseEntity
{
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    
    [Timestamp]
    public uint Version { get; set; }
}