using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace RouteFinder.Features.Route;

public sealed class RouteRateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    [Required]
    [ValidateObjectMembers]
    public RouteRateLimitPolicyOptions Generate { get; set; } = new();
}

public sealed class RouteRateLimitPolicyOptions
{
    [Range(1, 500)]
    public int PermitLimit { get; set; } = 6;

    [Range(1, 3600)]
    public int WindowSeconds { get; set; } = 60;
}

[OptionsValidator]
internal sealed partial class RouteRateLimitingOptionsValidator : IValidateOptions<RouteRateLimitingOptions>;
