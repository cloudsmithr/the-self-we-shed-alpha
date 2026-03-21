using System.ComponentModel.DataAnnotations;

namespace ApiServer.Models;

public class Player
{
    [Key]
    public Guid Id { get; set; }
    public required string DisplayName { get; set; }
    public required string OAuthProvider { get; set; }
    public required string OAuthId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastActiveAt { get; set; }
}