using System.ComponentModel.DataAnnotations;

namespace SmallSafe.Web.Data.Models;

public class UserAccount
{
    public int UserAccountId { get; set; }
    [Required]
    public string? AuthenticationUri { get; set; }
    public DateTime CreatedDateTime { get; set; } = DateTime.UtcNow;
    public DateTime? LastUpdateDateTime { get; set; }
    public DateTime? DeletedDateTime { get; set; }
    public string? TwoFactorKey { get; set; }
    public string? SafeDb { get; set; }

    public bool IsAccountConfigured =>
        DeletedDateTime == null &&
        !string.IsNullOrEmpty(TwoFactorKey) &&
        !string.IsNullOrEmpty(SafeDb);
}
