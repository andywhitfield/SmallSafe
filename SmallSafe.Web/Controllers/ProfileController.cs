using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmallSafe.Web.Authorization;
using SmallSafe.Web.Services;
using SmallSafe.Web.ViewModels.Profile;

namespace SmallSafe.Web.Controllers;

[Authorize(Policy = TwoFactorRequirement.PolicyName)]
public class ProfileController : Controller
{
    private readonly IUserService _userService;

    public ProfileController(IUserService userService) => _userService = userService;

    [HttpGet("~/profile")]
    public async Task<IActionResult> Index()
    {
        var user = await _userService.GetUserAsync(User);
        return View(new IndexViewModel(HttpContext));
    }
}
