using System.ComponentModel.DataAnnotations;

namespace ApiServer.Models;

public class Building : BaseEntity
{
    public Guid Id { get; set; }

    public Guid FortressId { get; set; }
    public Fortress Fortress { get; set; } = null!;

    public Guid BuildingTypeId { get; set; }
    public BuildingType BuildingType { get; set; } = null!;

    public int TotalUnits { get; set; }

    public Guid BuiltById { get; set; }
    public Player BuiltBy { get; set; } = null!;

    public DateTime BuiltAt { get; set; }
}
