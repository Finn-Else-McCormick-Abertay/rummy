
using System;

namespace Rummy.Config;
#nullable enable

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class ExportDescriptionAttribute(
        string? description = null,
        string? tooltip = null,
        string? displayName = null,
        string? type = null
    ) : Attribute
{
    public string? DisplayName => displayName;
    public string? Tooltip => tooltip;
    public string? Description => description;
    public string? Type => type;
}
