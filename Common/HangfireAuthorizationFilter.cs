using Hangfire.Dashboard;

namespace TunewaveAPIDB1.Common
{
    public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            // In production, add proper authentication here
            // For now, allow access (you should secure this)
            return true;
        }
    }
}


