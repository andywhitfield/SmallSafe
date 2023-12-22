using System.Security.Claims;
using SmallSafe.Web.Data.Models;

namespace SmallSafe.Web.Services;

public interface IUserService
{
    Task<bool> IsNewUserAsync(ClaimsPrincipal user);
    Task<UserAccount> GetUserAsync(ClaimsPrincipal user);
    Task<UserAccount?> GetUserByEmailAsync(string email);    
    Task<UserAccount> CreateUserAsync(string email, byte[] credentialId, byte[] publicKey, byte[] userHandle);
    Task UpdateUserDbAsync(UserAccount user, string safeDb);
    Task UpdateUserDropboxAsync(UserAccount user, string? dropboxAccessToken, string? dropboxRefreshToken);
    Task LoginSuccessAsync(UserAccount user);
    Task LoginFailureAsync(UserAccount user);
    IAsyncEnumerable<UserAccountCredential> GetUserCredentialsAsync(UserAccount user);
    Task<UserAccountCredential?> GetUserCredentialByUserHandleAsync(byte[] userHandle);
    Task SetSignatureCountAsync(UserAccountCredential userAccountCredential, uint signatureCount);
}