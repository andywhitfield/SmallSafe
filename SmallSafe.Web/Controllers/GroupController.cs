using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmallSafe.Web.ViewModels.Group;

namespace SmallSafe.Web.Controllers;

[Authorize(Policy = "TwoFactor")]
public class GroupController : Controller
{
    [HttpGet("~/group/{groupId}")]
    public IActionResult Index(int groupId)
    {
        return View(new IndexViewModel(HttpContext));
    }
}