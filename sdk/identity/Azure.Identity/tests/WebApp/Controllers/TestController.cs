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

        public TestController(ILogger<TestController> logger)
        {
            _logger = logger;
        }

        [HttpGet(Name = "GetTest")]
        public async Task<StatusCodeResult> Get()
        {
            var credential = new ManagedIdentityCredential();
            try
            {
                await credential.GetTokenAsync(_tokenRequestContext, default);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest();
            }
        }
    }
}
