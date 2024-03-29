using Google.Authenticator;
using SmallSafe.Web.Data.Models;

namespace SmallSafe.Web.Services;

public class TwoFactor : ITwoFactor
{
    public (string QrCodeImageUrl, string ManualSetupKey) GenerateSetupCodeForUser(UserAccount user)
    {
        TwoFactorAuthenticator tfa = new();
        var setupInfo = tfa.GenerateSetupCode("smallsafe.nosuchblogger.com", user.Email, user.TwoFactorKey, false, 3);
        return (setupInfo.QrCodeSetupImageUrl, setupInfo.ManualEntryKey);
    }

    public bool ValidateTwoFactorCodeForUser(UserAccount user, string? twofa)
    {
        TwoFactorAuthenticator tfa = new();
        return twofa != null && tfa.ValidateTwoFactorPIN(user.TwoFactorKey, twofa);
    }
}