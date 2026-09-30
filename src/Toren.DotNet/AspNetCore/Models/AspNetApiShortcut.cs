namespace Toren.DotNet.AspNetCore.Models;

public enum AspNetApiShortcutKind
{
    SwaggerUi = 0,
    OpenApiDocument = 1,
}

public sealed record AspNetApiShortcut(
    AspNetApiShortcutKind Kind,
    string DisplayName,
    Uri Uri);
