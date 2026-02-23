# Bulk Upload Excel/CSV Template

## File Format

The bulk upload accepts Excel (.xlsx, .xls) or CSV (.csv) files with the following columns:

### Column Structure

| Column | Field Name | Required | Description | Example |
|--------|-----------|----------|-------------|---------|
| A | ReleaseTitle | ✅ Yes | Release title | "Summer Hits 2024" |
| B | ReleaseTitleVersion | No | Release title version | "Deluxe Edition" |
| C | LabelId | ✅ Yes | Label ID | 10 |
| D | ReleaseDescription | No | Release description | "Collection of summer hits" |
| E | PrimaryGenre | No | Primary genre | "Pop" |
| F | SecondaryGenre | No | Secondary genre | "Electronic" |
| G | DigitalReleaseDate | No | Digital release date | 2024-06-01 |
| H | OriginalReleaseDate | No | Original release date | 2024-05-15 |
| I | UPCCode | No | UPC code | 123456789012 |
| J | TrackTitle | ✅ Yes | Track title | "Summer Vibes" |
| K | TrackVersion | No | Track version | "Radio Edit" |
| L | PrimaryArtistIds | No | Primary artist IDs (comma-separated) | 1,2,3 |
| M | FeaturedArtistIds | No | Featured artist IDs (comma-separated) | 4,5 |
| N | ComposerIds | No | Composer IDs (comma-separated) | 6 |
| O | LyricistIds | No | Lyricist IDs (comma-separated) | 7 |
| P | ProducerIds | No | Producer IDs (comma-separated) | 8,9 |
| Q | ISRC | No | ISRC code | USRC17607839 |
| R | TrackNumber | No | Track number | 1 |
| S | Language | No | Language code | "en" |
| T | IsExplicit | No | Explicit content (Yes/No, Y/N, 1/0, true/false) | Yes |
| U | IsInstrumental | No | Instrumental (Yes/No, Y/N, 1/0, true/false) | No |
| V | TrackGenre | No | Track genre | "Pop" |
| W | DurationSeconds | No | Duration in seconds | 180 |

### Sample Excel Row

```
ReleaseTitle | ReleaseTitleVersion | LabelId | ReleaseDescription | PrimaryGenre | SecondaryGenre | DigitalReleaseDate | OriginalReleaseDate | UPCCode | TrackTitle | TrackVersion | PrimaryArtistIds | FeaturedArtistIds | ComposerIds | LyricistIds | ProducerIds | ISRC | TrackNumber | Language | IsExplicit | IsInstrumental | TrackGenre | DurationSeconds
Summer Hits 2024 | Deluxe Edition | 10 | Collection of summer hits | Pop | Electronic | 2024-06-01 | 2024-05-15 | 123456789012 | Summer Vibes | Radio Edit | 1,2,3 | 4,5 | 6 | 7 | 8,9 | USRC17607839 | 1 | en | Yes | No | Pop | 180
```

### Notes

- **Required Fields**: ReleaseTitle, TrackTitle, LabelId
- **Date Format**: YYYY-MM-DD or Excel date format
- **Boolean Values**: Accepts Yes/No, Y/N, 1/0, true/false
- **Artist IDs**: Comma-separated list (e.g., "1,2,3")
- **CSV Format**: Use comma as delimiter, enclose values with commas in quotes

### Example CSV Row

```csv
"Summer Hits 2024","Deluxe Edition",10,"Collection of summer hits","Pop","Electronic","2024-06-01","2024-05-15","123456789012","Summer Vibes","Radio Edit","1,2,3","4,5","6","7","8,9","USRC17607839",1,"en","Yes","No","Pop",180
```


