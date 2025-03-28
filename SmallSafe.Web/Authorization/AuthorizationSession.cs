using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using SmallSafe.Web.Data.Models;
using SmallSafe.Web.Services;

namespace SmallSafe.Web.Authorization;

public class AuthorizationSession(ILogger<AuthorizationSession> logger,
    IUserService userService, IFido2 fido2,
    IHttpContextAccessor httpContextAccessor)
    : IAuthorizationSession
{
    public string MasterPassword => (IsValidated ? httpContextAccessor.HttpContext!.Session.GetString("pw") : null)
        ?? throw new InvalidOperationException("Authorization session has not been validated");

    public bool IsValidated
    {
        get
        {
            var ticks = httpContextAccessor.HttpContext?.Session.Get("dt");
            if (ticks == null)
                return false;
            
            DateTime createdDate = new(BitConverter.ToInt64(ticks));
            // to be valid, the session start time shouldn't be in the future and shouldn't be older than an hour
            return createdDate < DateTime.UtcNow && (DateTime.UtcNow - createdDate) <= TimeSpan.FromHours(1);
        }
    }

    public void Validate(string masterPassword)
    {
        var session = httpContextAccessor.HttpContext?.Session ?? throw new InvalidOperationException("Cannot access the auth session");
        session.Set("dt", BitConverter.GetBytes(DateTime.UtcNow.Ticks));
        session.SetString("pw", masterPassword);
    }

        public async Task<(bool IsReturningUser, string VerifyOptions)> HandleSigninRequest(string email, CancellationToken cancellationToken)
    {
        UserAccount? user;
        string options;
        if ((user = await userService.GetUserByEmailAsync(email)) != null)
        {
            logger.LogTrace($"Found existing user account with email [{email}], creating assertion options");
            options = fido2.GetAssertionOptions(
                await userService
                    .GetUserCredentialsAsync(user)
                    .Select(uac => new PublicKeyCredentialDescriptor(uac.CredentialId))
                    .ToArrayAsync(cancellationToken: cancellationToken),
                UserVerificationRequirement.Discouraged
            ).ToJson();
        }
        else
        {
            logger.LogTrace($"Found no user account with email [{email}], creating request new creds options");
            options = fido2.RequestNewCredential(
                new Fido2User() { Id = Encoding.UTF8.GetBytes(email), Name = email, DisplayName = email },
                [],
                AuthenticatorSelection.Default,
                AttestationConveyancePreference.None
            ).ToJson();
        }

        logger.LogTrace($"Created sign in options: {options}");

        return (user != null, options);        
    }

    public async Task<bool> HandleSigninVerifyRequest(HttpContext httpContext, string email, string verifyOptions, string verifyResponse, CancellationToken cancellationToken)
    {
        UserAccount? user;
        if ((user = await userService.GetUserByEmailAsync(email)) != null)
        {
            if (!await SigninUserAsync(user, verifyOptions, verifyResponse, cancellationToken))
                return false;
        }
        else
        {
            user = await CreateNewUserAsync(email, verifyOptions, verifyResponse, cancellationToken);
            if (user == null)
                return false;
        }

        List<Claim> claims = [new Claim(ClaimTypes.Name, user.Email)];
        ClaimsIdentity claimsIdentity = new(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        AuthenticationProperties authProperties = new() { IsPersistent = true };
        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

        logger.LogTrace($"Signed in: {email}");

        return true;
    }

    private async Task<UserAccount?> CreateNewUserAsync(string email, string verifyOptions, string verifyResponse, CancellationToken cancellationToken)
    {
        logger.LogTrace("Creating new user credientials");
        var options = CredentialCreateOptions.FromJson(verifyOptions);

        AuthenticatorAttestationRawResponse? authenticatorAttestationRawResponse = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(verifyResponse);
        if (authenticatorAttestationRawResponse == null)
        {
            logger.LogWarning($"Cannot parse signin verify response: {verifyResponse}");
            return null;
        }

        logger.LogTrace($"Successfully parsed response: {verifyResponse}");

        var success = await fido2.MakeNewCredentialAsync(authenticatorAttestationRawResponse, options, (_, _) => Task.FromResult(true), cancellationToken: cancellationToken);
        logger.LogInformation($"got success status: {success.Status} error: {success.ErrorMessage}");
        if (success.Result == null)
        {
            logger.LogWarning($"Could not create new credential: {success.Status} - {success.ErrorMessage}");
            return null;
        }

        logger.LogTrace($"Got new credential: {JsonSerializer.Serialize(success.Result)}");

        return await userService.CreateUserAsync(email, success.Result.CredentialId,
            success.Result.PublicKey, success.Result.User.Id);
    }

    private async Task<bool> SigninUserAsync(UserAccount user, string verifyOptions, string verifyResponse, CancellationToken cancellationToken)
    {
        logger.LogTrace($"Checking credientials: {verifyResponse}");
        AuthenticatorAssertionRawResponse? authenticatorAssertionRawResponse = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(verifyResponse);
        if (authenticatorAssertionRawResponse == null)
        {
            logger.LogWarning($"Cannot parse signin assertion verify response: {verifyResponse}");
            return false;
        }
        var options = AssertionOptions.FromJson(verifyOptions);
        var userAccountCredential = await userService.GetUserCredentialsAsync(user).FirstOrDefaultAsync(uac => uac.CredentialId.SequenceEqual(authenticatorAssertionRawResponse.Id), cancellationToken);
        if (userAccountCredential == null)
        {
            logger.LogWarning($"No credential id [{Convert.ToBase64String(authenticatorAssertionRawResponse.Id)}] for user [{user.Email}]");
            return false;
        }
        
        logger.LogTrace($"Making assertion for user [{user.Email}]");
        var res = await fido2.MakeAssertionAsync(authenticatorAssertionRawResponse, options, userAccountCredential.PublicKey, userAccountCredential.SignatureCount, VerifyExistingUserCredentialAsync, cancellationToken: cancellationToken);
        if (!string.IsNullOrEmpty(res.ErrorMessage))
        {
            logger.LogWarning($"Signin assertion failed: {res.Status} - {res.ErrorMessage}");
            return false;
        }

        logger.LogTrace($"Signin success, got response: {JsonSerializer.Serialize(res)}");
        await userService.SetSignatureCountAsync(userAccountCredential, res.Counter);

        return true;
    }

    private async Task<bool> VerifyExistingUserCredentialAsync(IsUserHandleOwnerOfCredentialIdParams credentialIdUserHandleParams, CancellationToken cancellationToken)
    {
        logger.LogInformation($"Checking credential {credentialIdUserHandleParams.CredentialId} - {credentialIdUserHandleParams.UserHandle}");
        var userAccountCredentials = await userService.GetUserCredentialByUserHandleAsync(credentialIdUserHandleParams.UserHandle);
        return userAccountCredentials?.CredentialId.SequenceEqual(credentialIdUserHandleParams.CredentialId) ?? false;
    }
}