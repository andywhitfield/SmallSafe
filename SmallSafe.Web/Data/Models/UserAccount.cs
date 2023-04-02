using System.ComponentModel.DataAnnotations;

namespace SmallSafe.Web.Data.Models;

public class UserAccount
{
    public int UserAccountId { get; set; }
    [Required]
    public string? AuthenticationUri { get; set; }
    public int? LastSelectedUserListId { get; set; }
    public DateTime CreatedDateTime { get; set; } = DateTime.UtcNow;
    public DateTime? LastUpdateDateTime { get; set; }
    public DateTime? DeletedDateTime { get; set; }
}
