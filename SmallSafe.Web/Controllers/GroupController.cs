using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmallSafe.Web.Authorization;
using SmallSafe.Web.ViewModels.Group;

namespace SmallSafe.Web.Controllers;

[Authorize(Policy = TwoFactorRequirement.PolicyName)]
public class GroupController : Controller
{
    [HttpGet("~/group/{groupId}")]
    public IActionResult Index(int groupId)
    {
        return View(new IndexViewModel(HttpContext));
    }
}