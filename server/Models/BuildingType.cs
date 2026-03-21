using System.ComponentModel.DataAnnotations;

namespace ApiServer.Models;

public class BuildingType
{
    public Guid Id { get; set; }

    public Guid GameWorldId { get; set; }
    public GameWorld GameWorld { get; set; } = null!;

    public string Name { get; set; } = null!;

    public float ProductionRate { get; set; }

    public int UnlockTier { get; set; }

    public string Description { get; set; } = null!;

    public ICollection<Building> Buildings { get; set; } = new List<Building>();

#pragma warning disable CS8618
    [Timestamp]
    public byte[] Version { get; set; }
#pragma warning restore CS8618
}
