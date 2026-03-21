using System.ComponentModel.DataAnnotations;

namespace ApiServer.Models;

public class GameWorld
{
    [Key]
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Status { get; set; }
    public required string Config { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    public ICollection<Fortress> Fortresses { get; set; } = new List<Fortress>();
}