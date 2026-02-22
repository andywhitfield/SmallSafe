using Microsoft.AspNetCore.Mvc;
using SmallSafe.Secure;

namespace SmallSafe.Web.Controllers.Api;

[ApiController]
public class GeneratePasswordApiController(IRandomPasswordGenerator randomPasswordGenerator) : ControllerBase
{
    [HttpGet("~/api/generatepassword")]
    public ActionResult GeneratePasswords([FromQuery] int? genpwmin = null, [FromQuery] int? genpwmax = null, [FromQuery] bool? genpwnums = null, [FromQuery] bool? genpwallchars = null)
    {
        if (genpwmin != null)
            randomPasswordGenerator.MinimumLength = genpwmin.Value;
        if (genpwmax != null)
            randomPasswordGenerator.MaximumLength = genpwmax.Value;
        if (genpwnums != null)
            randomPasswordGenerator.AllowNumbers = genpwnums.Value;
        if (genpwallchars != null)
            randomPasswordGenerator.AllowPunctuation = genpwallchars.Value;
        
        return Ok(Enumerable.Range(0, 10).Select(_ => randomPasswordGenerator.Generate()));
    }
}