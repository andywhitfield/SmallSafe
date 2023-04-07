using SmallSafe.Web.Data.Models;

namespace SmallSafe.Web.Services;

public interface ITwoFactor
{
    (string QrCodeImageUrl, string ManualSetupKey) GenerateSetupCodeForUser(UserAccount user);
    bool ValidateTwoFactorCodeForUser(UserAccount user, string twofa);
}
