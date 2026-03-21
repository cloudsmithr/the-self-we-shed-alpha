using ApiServer.Enums;

namespace ApiServer.Models;

public class PlayerContribution
{
    public Guid Id { get; set; }

    public Guid PlayerId { get; set; }
    public Player Player { get; set; } = null!;

    public Guid GameWorldId { get; set; }
    public GameWorld GameWorld { get; set; } = null!;

    public PlayerActionType ActionType { get; set; }

    public Guid TargetFortressId { get; set; }
    public Fortress TargetFortress { get; set; } = null!;

    public Guid BuildingTypeId { get; set; }
    public BuildingType BuildingType { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}
