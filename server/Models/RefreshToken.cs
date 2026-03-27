namespace ApiServer.Models;


public class RefreshToken : BaseEntity
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// SHA-256 hash of the token sent to the client. Never store raw tokens.
    /// </summary>
    public string TokenHash { get; set; } = null!;
    
    public Guid PlayerId { get; set; }
    public Player Player { get; set; } = null!;
    
    public Guid FamilyId { get; set; }
    
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    
    public bool IsActive => ConsumedAt is null && RevokedAt is null && ExpiresAt > DateTime.UtcNow;

}