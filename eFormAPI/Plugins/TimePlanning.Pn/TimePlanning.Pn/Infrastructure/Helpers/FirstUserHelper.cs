using System.Threading.Tasks;
using Microting.eFormApi.BasePn.Abstractions;

namespace TimePlanning.Pn.Infrastructure.Helpers;

/// <summary>
/// Mirrors eFormAPI.Web's Infrastructure.Helpers.FirstUserHelper: the first
/// user is the account with the lowest AspNetUsers Id. A caller without a
/// user id is never the first user, even when the users table is empty (in
/// which case GetFirstUserIdInDb would also answer 0, and 0 == 0 must not
/// mean "everyone is the first user").
/// </summary>
public static class FirstUserHelper
{
    public static async Task<bool> IsFirstUserAsync(this IUserService userService)
    {
        var userId = userService.UserId;
        return userId > 0 && userId == await userService.GetFirstUserIdInDb();
    }
}
