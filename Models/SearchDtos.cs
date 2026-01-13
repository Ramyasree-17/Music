namespace TunewaveAPIDB1.Models
{
    public class SearchQueryDto
    {
        [System.ComponentModel.DataAnnotations.Required]
        public string Q { get; set; } = string.Empty;
        public string Type { get; set; } = "all";
        public int Limit { get; set; } = 20;
        public int Page { get; set; } = 1;
        public string? Language { get; set; }
        public string? Genre { get; set; }
        public int? LabelId { get; set; }
        public DateTime? ReleaseDateFrom { get; set; }
        public DateTime? ReleaseDateTo { get; set; }
        public string? Status { get; set; }
        public int? EnterpriseId { get; set; }
        public string? Sort { get; set; }
    }

    public class SearchResultDto
    {
        public int? ArtistId { get; set; }
        public int? ReleaseId { get; set; }
        public int? LabelId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? StageName { get; set; }
        public string? LabelName { get; set; }
        public double Score { get; set; }
        public string? ProfileUrl { get; set; }
        public List<int>? PrimaryLabels { get; set; }
        public bool? IsClaimed { get; set; }
    }

    public class SearchResponse
    {
        public string Q { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalHits { get; set; }
        public List<SearchResultDto> Results { get; set; } = new();
        public int TookMs { get; set; }
    }

    public class SuggestResponse
    {
        public string Q { get; set; } = string.Empty;
        public List<SuggestionDto> Suggestions { get; set; } = new();
    }

    public class SuggestionDto
    {
        public string Token { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int? ReferenceId { get; set; }
        public bool IsExactMatch { get; set; }
    }
}
























