using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace TunewaveAPIDB1.Controllers
{
    [ApiController]
    [Route("api/health")]
    public class HealthController : ControllerBase
    {
        private readonly IConfiguration _cfg;

        public HealthController(IConfiguration cfg)
        {
            _cfg = cfg;
        }

        [HttpGet]
        public IActionResult GetHealth()
        {
            try
            {
                var connStr = _cfg.GetConnectionString("DefaultConnection");
                
                if (string.IsNullOrEmpty(connStr))
                {
                    return StatusCode(503, new
                    {
                        status = "unhealthy",
                        message = "Database connection string not configured"
                    });
                }

                // Test database connection
                using var conn = new SqlConnection(connStr);
                conn.Open();
                
                using var cmd = new SqlCommand("SELECT 1", conn);
                cmd.ExecuteScalar();

                return Ok(new
                {
                    status = "healthy",
                    timestamp = DateTime.UtcNow,
                    database = "connected"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(503, new
                {
                    status = "unhealthy",
                    message = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }
    }
}

