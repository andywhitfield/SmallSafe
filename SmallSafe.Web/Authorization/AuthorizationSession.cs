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
            logger.LogTrace("Found existing user account with email [{Email}], creating assertion options", email);
            options = fido2.GetAssertionOptions(new()
            {
                AllowedCredentials = await userService
                    .GetUserCredentialsAsync(user)
                    .Select(uac => new PublicKeyCredentialDescriptor(uac.CredentialId))
                    .ToArrayAsync(cancellationToken: cancellationToken),
                UserVerification = UserVerificationRequirement.Discouraged
            }).ToJson();
        }
        else
        {
            logger.LogTrace("Found no user account with email [{Email}], creating request new creds options", email);
            options = fido2.RequestNewCredential(new()
            {
                User = new Fido2User() { Id = Encoding.UTF8.GetBytes(email), Name = email, DisplayName = email },
                ExcludeCredentials = [],
                AuthenticatorSelection = AuthenticatorSelection.Default,
                AttestationPreference = AttestationConveyancePreference.None,
                PubKeyCredParams = []
            }).ToJson();
        }

        logger.LogTrace("Created sign in options: {Options}", options);

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

        logger.LogTrace("Signed in: {Email}", email);

        return true;
    }

    private async Task<UserAccount?> CreateNewUserAsync(string email, string verifyOptions, string verifyResponse, CancellationToken cancellationToken)
    {
        logger.LogTrace("Creating new user credientials");
        var options = CredentialCreateOptions.FromJson(verifyOptions);

        AuthenticatorAttestationRawResponse? authenticatorAttestationRawResponse = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(verifyResponse);
        if (authenticatorAttestationRawResponse == null)
        {
            logger.LogWarning("Cannot parse signin verify response: {VerifyResponse}", verifyResponse);
            return null;
        }

        logger.LogTrace("Successfully parsed response: {verifyResponse}", verifyResponse);

        var success = await fido2.MakeNewCredentialAsync(new() { AttestationResponse = authenticatorAttestationRawResponse, OriginalOptions = options, IsCredentialIdUniqueToUserCallback = (_, _) => Task.FromResult(true) }, cancellationToken);
        if (success == null)
        {
            logger.LogWarning("Could not create new credential");
            return null;
        }
        logger.LogInformation("got success status");

        logger.LogTrace("Got new credential: {Result}", JsonSerializer.Serialize(success));

        return await userService.CreateUserAsync(email, success.Id,
            success.PublicKey, success.User.Id);
    }

    private async Task<bool> SigninUserAsync(UserAccount user, string verifyOptions, string verifyResponse, CancellationToken cancellationToken)
    {
        logger.LogTrace("Checking credientials: {VerifyResponse}", verifyResponse);
        AuthenticatorAssertionRawResponse? authenticatorAssertionRawResponse = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(verifyResponse);
        if (authenticatorAssertionRawResponse == null)
        {
            logger.LogWarning("Cannot parse signin assertion verify response: {VerifyResponse}", verifyResponse);
            return false;
        }
        var options = AssertionOptions.FromJson(verifyOptions);
        var userAccountCredential = await userService.GetUserCredentialsAsync(user).FirstOrDefaultAsync(uac => uac.CredentialId.SequenceEqual(authenticatorAssertionRawResponse.RawId), cancellationToken);
        if (userAccountCredential == null)
        {
            logger.LogWarning("No credential id [{Id}] for user [{UserEmail}]", Convert.ToBase64String(authenticatorAssertionRawResponse.RawId), user.Email);
            return false;
        }
        
        logger.LogTrace("Making assertion for user [{UserEmail}]", user.Email);
        var res = await fido2.MakeAssertionAsync(new()
        {
            AssertionResponse = authenticatorAssertionRawResponse,
            OriginalOptions = options,
            StoredPublicKey = userAccountCredential.PublicKey,
            StoredSignatureCounter = userAccountCredential.SignatureCount,
            IsUserHandleOwnerOfCredentialIdCallback = VerifyExistingUserCredentialAsync
        }, cancellationToken: cancellationToken);
        if (res == null)
        {
            logger.LogWarning("Signin assertion failed: {Res}", res);
            return false;
        }

        logger.LogTrace("Signin success, got response: {Res}", JsonSerializer.Serialize(res));
        await userService.SetSignatureCountAsync(userAccountCredential, res.SignCount);

        return true;
    }

    private async Task<bool> VerifyExistingUserCredentialAsync(IsUserHandleOwnerOfCredentialIdParams credentialIdUserHandleParams, CancellationToken cancellationToken)
    {
        logger.LogInformation("Checking credential {CredentialId} - {UserHandle}", credentialIdUserHandleParams.CredentialId, credentialIdUserHandleParams.UserHandle);
        var userAccountCredentials = await userService.GetUserCredentialByUserHandleAsync(credentialIdUserHandleParams.UserHandle);
        return userAccountCredentials?.CredentialId.SequenceEqual(credentialIdUserHandleParams.CredentialId) ?? false;
    }
}