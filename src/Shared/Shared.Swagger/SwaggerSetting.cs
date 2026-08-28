using System.Collections.Generic;

namespace Shared.Swagger;

public sealed class SwaggerSetting
{
    public const string SectionName = "SwaggerSetting";

    public bool Enabled { get; set; } = true;
    public string ErrorMessage { get; set; } = "Swagger is disabled by configuration.";
    public string Name { get; set; } = "FSMS APIs";
    public string Title { get; set; } = "Field Survey Management System (FSMS) API";
    public string Description { get; set; } = "Field Survey Management System (FSMS) APIs Management";
    public List<SwaggerVersionSetting> Version { get; set; } = new();
}

public sealed class SwaggerVersionSetting
{
    public string URL { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
}
