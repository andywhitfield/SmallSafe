using Microsoft.AspNetCore.Mvc;
using SmallSafe.Secure;

namespace SmallSafe.Web.Controllers.Api;

[ApiController]
public class GeneratePasswordApiController : ControllerBase
{
    private readonly ILogger<GeneratePasswordApiController> _logger;
    private readonly IRandomPasswordGenerator _randomPasswordGenerator;

    public GeneratePasswordApiController(ILogger<GeneratePasswordApiController> logger, IRandomPasswordGenerator randomPasswordGenerator)
    {
        _logger = logger;
        _randomPasswordGenerator = randomPasswordGenerator;
    }

    [HttpGet("~/api/generatepassword")]
    public ActionResult GeneratePasswords([FromQuery] int? genpwmin = null, [FromQuery] int? genpwmax = null, [FromQuery] bool? genpwnums = null, [FromQuery] bool? genpwallchars = null)
    {
        if (genpwmin != null)
            _randomPasswordGenerator.MinimumLength = genpwmin.Value;
        if (genpwmax != null)
            _randomPasswordGenerator.MaximumLength = genpwmax.Value;
        if (genpwnums != null)
            _randomPasswordGenerator.AllowNumbers = genpwnums.Value;
        if (genpwallchars != null)
            _randomPasswordGenerator.AllowPunctuation = genpwallchars.Value;
        
        return Ok(Enumerable.Range(0, 10).Select(_ => _randomPasswordGenerator.Generate()));
    }
}