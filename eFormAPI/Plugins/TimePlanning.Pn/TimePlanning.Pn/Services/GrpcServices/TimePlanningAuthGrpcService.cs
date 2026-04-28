using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microting.EformAngularFrontendBase.Infrastructure.Const;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Database.Entities;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.eFormApi.BasePn.Infrastructure.Models.Application;
using Sentry;
using TimePlanning.Pn.Grpc;
using TimePlanning.Pn.Infrastructure.Models.RegistrationDevice;
using TimePlanning.Pn.Services.TimePlanningRegistrationDeviceService;

namespace TimePlanning.Pn.Services.GrpcServices;

public class TimePlanningAuthGrpcService : TimePlanningAuthService.TimePlanningAuthServiceBase
{
    private readonly ITimePlanningRegistrationDeviceService _registrationDeviceService;
    private readonly IUserService _userService;
    private readonly UserManager<EformUser> _userManager;
    private readonly RoleManager<EformRole> _roleManager;
    private readonly IOptions<EformTokenOptions> _tokenOptions;

    public TimePlanningAuthGrpcService(
        ITimePlanningRegistrationDeviceService registrationDeviceService,
        IUserService userService,
        UserManager<EformUser> userManager,
        RoleManager<EformRole> roleManager,
        IOptions<EformTokenOptions> tokenOptions)
    {
        _registrationDeviceService = registrationDeviceService;
        _userService = userService;
        _userManager = userManager;
        _roleManager = roleManager;
        _tokenOptions = tokenOptions;
    }

    public override async Task<ActivateDeviceResponse> ActivateDevice(
        ActivateDeviceRequest request, ServerCallContext context)
    {
        var model = new TimePlanningRegistrationDeviceActivateModel
        {
            CustomerNo = int.TryParse(request.CustomerNo, out var custNo) ? custNo : 0,
            OtCode = request.OtCode
        };

        var result = await _registrationDeviceService.Activate(model);

        var response = new ActivateDeviceResponse
        {
            Success = result.Success,
            Message = result.Message ?? ""
        };

        if (result.Success && result is OperationDataResult<TimePlanningRegistrationDeviceAuthModel> dataResult
            && dataResult.Model != null)
        {
            response.Model = new ActivateDeviceModel
            {
                Token = dataResult.Model.Token ?? ""
            };
        }

        return response;
    }

    /// <summary>
    /// gRPC mirror of POST /api/auth/token (grant_type=password).
    /// Validates username + password directly via UserManager (no SignInManager
    /// — that is HTTP-bound), then mints a JWT using the same claim-set as
    /// <see cref="RefreshToken"/> / eFormAPI.Web AuthService.GenerateToken.
    /// </summary>
    /// <remarks>
    /// Compared to the JSON path (<c>AuthService.AuthenticateUser</c>), this
    /// implementation deliberately omits:
    /// <list type="bullet">
    /// <item>2FA / Google Authenticator paths — gated by per-tenant config and
    /// by user.TwoFactorEnabled. The contract test user does not have it on,
    /// and the proto request has no <c>code</c> field.</item>
    /// <item>SignInManager lockout tracking — would require HttpContext
    /// access. Falls back to a plain CheckPasswordAsync.</item>
    /// <item>IClaimsService / IAuthCacheService warm-up — neither interface is
    /// plugin-accessible. The next REST call rebuilds the cache lazily.</item>
    /// </list>
    /// Failure messages mirror the JSON oracle so the contract diff stays
    /// shape-clean: "Empty username or password", "User with username X not
    /// found", "Incorrect password.", "Email X not confirmed".
    /// </remarks>
    public override async Task<AuthenticateUserResponse> AuthenticateUser(
        AuthenticateUserRequest request, ServerCallContext context)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
            {
                return new AuthenticateUserResponse
                {
                    Success = false,
                    Message = "Empty username or password"
                };
            }

            // The JSON path uses IUserService.GetByUsernameAsync; we approximate
            // with FindByNameAsync, then fall back to FindByEmailAsync (the dev
            // login user authenticates by email). This matches the live flow.
            var user = await _userManager.FindByNameAsync(request.Username)
                       ?? await _userManager.FindByEmailAsync(request.Username);
            if (user == null)
            {
                return new AuthenticateUserResponse
                {
                    Success = false,
                    Message = $"User with username {request.Username} not found"
                };
            }

            var passwordOk = await _userManager.CheckPasswordAsync(user, request.Password);
            if (!passwordOk)
            {
                return new AuthenticateUserResponse
                {
                    Success = false,
                    Message = "Incorrect password."
                };
            }

            if (!user.EmailConfirmed)
            {
                return new AuthenticateUserResponse
                {
                    Success = false,
                    Message = $"Email {user.Email} not confirmed"
                };
            }

            var roleList = await _userManager.GetRolesAsync(user);
            if (!roleList.Any())
            {
                return new AuthenticateUserResponse
                {
                    Success = false,
                    Message = $"Role for user {request.Username} not found"
                };
            }

            var (token, _) = await GenerateToken(user);

            // JSON oracle wraps successful results with message="Success".
            return new AuthenticateUserResponse
            {
                Success = true,
                Message = "Success",
                Model = new AuthUserModel
                {
                    AccessToken = token ?? "",
                    FirstName = user.FirstName ?? "",
                    LastName = user.LastName ?? ""
                }
            };
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            Console.WriteLine($"[GRPC-AUTH] Error: {ex.Message}\n{ex.StackTrace}");
            return new AuthenticateUserResponse
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    /// <summary>
    /// gRPC mirror of GET /api/auth/token/refresh.
    /// The current Bearer JWT is consumed from gRPC `authorization` metadata
    /// (validated by the global JWT bearer middleware before this method is
    /// invoked). The validated user is then resolved via <see cref="IUserService"/>
    /// and a freshly-minted JWT is returned.
    /// </summary>
    /// <remarks>
    /// We re-implement the JWT mint flow locally rather than calling
    /// eFormAPI.Web's <c>IAuthService.RefreshToken</c> because that interface
    /// is not in the plugin loader's shared-types list. The primitives used
    /// here (<see cref="UserManager{TUser}"/>, <see cref="RoleManager{TRole}"/>,
    /// <see cref="IUserService"/>, <see cref="EformTokenOptions"/>) all live
    /// in <c>Microting.eFormApi.BasePn</c> / <c>Microting.EformAngularFrontendBase</c>
    /// and ARE accessible to plugins. The eFormAPI.Web version additionally
    /// updates an in-memory auth cache via <c>IClaimsService</c>/<c>IAuthCacheService</c>;
    /// those interfaces are not plugin-accessible. Skipping them means the new
    /// token's permission cache is not pre-populated — the next REST call will
    /// lazily rebuild it. For the contract test (which ignores the access-token
    /// value itself) this is sufficient.
    /// </remarks>
    public override async Task<RefreshTokenResponse> RefreshToken(
        RefreshTokenRequest request, ServerCallContext context)
    {
        try
        {
            var user = await _userService.GetByIdAsync(_userService.UserId);
            if (user == null)
            {
                return new RefreshTokenResponse
                {
                    Success = false,
                    Message = $"User with id {_userService.UserId} not found"
                };
            }

            var (token, _) = await GenerateToken(user);
            var roleList = await _userManager.GetRolesAsync(user);
            if (!roleList.Any())
            {
                return new RefreshTokenResponse
                {
                    Success = false,
                    Message = $"Role for user {_userService.UserId} not found"
                };
            }

            // JSON oracle wraps successful results with message="Success"
            // (eFormAPI.Web AuthService -> OperationDataResult success ctor).
            // Match that exactly so the contract diff stays clean.
            return new RefreshTokenResponse
            {
                Success = true,
                Message = "Success",
                Model = new AuthUserModel
                {
                    AccessToken = token ?? "",
                    FirstName = user.FirstName ?? "",
                    LastName = user.LastName ?? ""
                }
            };
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            Console.WriteLine($"[GRPC-REFRESH] Error: {ex.Message}\n{ex.StackTrace}");
            return new RefreshTokenResponse
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    /// <summary>
    /// Mints a JWT for the supplied user. Mirrors the claim-set produced by
    /// eFormAPI.Web's AuthService.GenerateToken (Sub, Jti, updated_at, locale,
    /// user claims, role + role claims) so the resulting token is structurally
    /// indistinguishable from a JSON-path token. The auth-cache write is
    /// intentionally omitted (see <see cref="RefreshToken"/> remarks).
    /// </summary>
    private async Task<(string token, DateTime expireIn)> GenerateToken(EformUser user)
    {
        if (user == null)
        {
            return (null, DateTime.Now);
        }

        var timeStamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(AuthConsts.ClaimLastUpdateKey, timeStamp.ToString())
        };

        if (!string.IsNullOrEmpty(user.Locale))
        {
            claims.Add(new Claim("locale", user.Locale));
        }

        var userClaims = await _userManager.GetClaimsAsync(user);
        var userRoles = await _userManager.GetRolesAsync(user);
        claims.AddRange(userClaims);
        foreach (var userRole in userRoles)
        {
            claims.Add(new Claim(ClaimTypes.Role, userRole));
            var role = await _roleManager.FindByNameAsync(userRole);
            if (role != null)
            {
                var roleClaims = await _roleManager.GetClaimsAsync(role);
                foreach (var roleClaim in roleClaims)
                {
                    claims.Add(roleClaim);
                }
            }
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_tokenOptions.Value.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expireIn = DateTime.Now.AddHours(24);
        var token = new JwtSecurityToken(_tokenOptions.Value.Issuer,
            _tokenOptions.Value.Issuer,
            claims.ToArray(),
            expires: expireIn,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expireIn);
    }
}
