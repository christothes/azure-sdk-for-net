using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Mvc;

namespace WebApp.Controllers
{

    [ApiController]
    [Route("[controller]")]
    public class TestController : ControllerBase
    {
        private readonly ILogger<TestController> _logger;
        private readonly TokenRequestContext _tokenRequestContext = new(new[] { "https://storage.azure.com/.default" });
        private readonly TokenCredential _credential;

        public TestController(ILogger<TestController> logger)
        {
            _logger = logger;
            _credential = new ManagedIdentityCredential();
        }

        [HttpGet(Name = "GetTest")]
        public async Task<IActionResult> Get()
        {
            try
            {
                await _credential.GetTokenAsync(_tokenRequestContext, default);
                // await Task.Delay(1);
                return Ok("Successfully acquired a token from ManagedIdentityCredential");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.ToString());
            }
        }
    }
}
