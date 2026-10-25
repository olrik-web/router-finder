using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace RouteFinder.Features.Route;

public sealed class OpenRouteServiceOptions
{
    public const string SectionName = "OpenRouteService";

    [Required]
    public required string ApiKey { get; set; }

    [Required]
    public string BaseUrl { get; set; } = "https://api.openrouteservice.org";

    [Range(1, 60)]
    public int TimeoutSeconds { get; set; } = 15;
}

[OptionsValidator]
internal sealed partial class OpenRouteServiceOptionsValidator : IValidateOptions<OpenRouteServiceOptions>;
