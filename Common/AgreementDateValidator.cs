using Microsoft.AspNetCore.Mvc;

namespace TunewaveAPIDB1.Common
{
    public static class AgreementDateValidator
    {
        /// <summary>
        /// Validates agreement dates:
        /// 1. End date must be greater than start date
        /// 2. Minimum 6 months (182 days) gap between start and end dates
        /// </summary>
        /// <param name="startDate">Agreement start date</param>
        /// <param name="endDate">Agreement end date</param>
        /// <returns>BadRequest IActionResult if validation fails, null if valid</returns>
        public static IActionResult? Validate(DateTime startDate, DateTime endDate)
        {
            // Condition 1: End date must be greater than start date
            if (endDate <= startDate)
            {
                return new BadRequestObjectResult(new
                {
                    error = "AgreementEndDate must be greater than AgreementStartDate. End date cannot be less than or equal to start date."
                });
            }

            // Condition 2: Minimum 6 months (182 days)
            var daysDifference = (endDate - startDate).TotalDays;
            const int minimumDays = 182; // 6 months minimum

            if (daysDifference < minimumDays)
            {
                return new BadRequestObjectResult(new
                {
                    error = $"AgreementEndDate must be at least {minimumDays} days (6 months) after AgreementStartDate. Current difference is {daysDifference:F0} days."
                });
            }

            return null; // valid
        }
    }
}



















