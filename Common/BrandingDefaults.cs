namespace TunewaveAPIDB1.Common
{
    public static class BrandingDefaults
    {
        public static string SiteName = "TuneWave";
        public static string SiteDescription = "Enterprise Audio Platform";

        public static string PrimaryColor = "#6366F1";
        public static string SecondaryColor = "#F59E0B";
        public static string HeaderColor = "#1F2937";
        public static string SidebarColor = "#111827";
        public static string FooterColor = "#0F172A";

        public static string DefaultLogo = "/logos/tunewave.png";

        public static string FooterText = "© 2026 TuneWave. All rights reserved.";

        public static object FooterLinks = new[]
        {
            new { title = "Privacy Policy", url = "/privacy" },
            new { title = "Terms", url = "/terms" }
        };
    }
}

