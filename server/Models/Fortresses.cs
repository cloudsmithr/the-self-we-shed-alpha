using System.ComponentModel.DataAnnotations;
using ApiServer.Enums;

namespace ApiServer.Models;

public class Fortress
{
    public Guid Id { get; set; }

    public Guid GameWorldId { get; set; }
    public GameWorld GameWorld { get; set; } = null!;

    public string Name { get; set; } = null!;

    public int PositionX { get; set; }
    public int PositionY { get; set; }

    public FortressOwner Owner { get; set; }
    
    public Guid? ParentId { get; set; }
    public Fortress? Parent { get; set; }

    public ICollection<Fortress> Children { get; set; } = new List<Fortress>();

    public int CaptureThreshold { get; set; }

    public int DifficultyTier { get; set; }

    public bool IsFinalObjective { get; set; }

#pragma warning disable CS8618
    [Timestamp]
    public byte[] Version { get; set; }
#pragma warning restore CS8618
}