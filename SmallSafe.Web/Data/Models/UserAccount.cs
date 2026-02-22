using System.ComponentModel.DataAnnotations;

namespace SmallSafe.Web.Data.Models;

public class UserAccount
{
    public int UserAccountId { get; set; }
    [Required]
    public required string Email { get; set; }
    public DateTime CreatedDateTime { get; set; } = DateTime.UtcNow;
    public DateTime? LastUpdateDateTime { get; set; }
    public DateTime? DeletedDateTime { get; set; }
    public string? TwoFactorKey { get; set; }
    public DateTime? LastTwoFactorSuccess { get; set; }
    public DateTime? LastTwoFactorFailure { get; set; }
    public int TwoFactorFailureCount { get; set; }
    public string? SafeDb { get; set; }
    public byte[]? EncyptedSafeDb { get; set; }
    public string? DropboxAccessToken { get; set; }
    public string? DropboxRefreshToken { get; set; }

    public bool IsAccountConfigured =>
        DeletedDateTime == null &&
        !string.IsNullOrEmpty(TwoFactorKey) &&
        EncyptedSafeDb != null;
    
    public bool IsConnectedToDropbox =>
        !string.IsNullOrEmpty(DropboxAccessToken) &&
        !string.IsNullOrEmpty(DropboxRefreshToken);
}
