namespace Artskart3.Core.Domain.BusinessModels;

/// <summary>
/// Enumeration for styling types used in visualization and display.
/// </summary>
public enum StyleType
{
    Unknown = 0,
    Category = 1,
    Precision = 2,
    Species = 3
}

/// <summary>
/// Business model for handling styling and visualization of data.
/// </summary>
public class StyleTypeModel
{
    public StyleType Style { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ColorCode { get; set; } = string.Empty;

    public StyleTypeModel()
    {
    }

    public StyleTypeModel(StyleType style)
    {
        Style = style;
        SetStyleProperties();
    }

    /// <summary>
    /// Sets the properties based on the style type.
    /// </summary>
    private void SetStyleProperties()
    {
        switch (Style)
        {
            case StyleType.Category:
                DisplayName = "Category Style";
                Description = "Visual styling based on observation category";
                break;
            case StyleType.Precision:
                DisplayName = "Precision Style";
                Description = "Visual styling based on coordinate precision";
                break;
            case StyleType.Species:
                DisplayName = "Species Style";
                Description = "Visual styling based on species/taxon";
                break;
            default:
                DisplayName = "Unknown Style";
                Description = "Unknown styling type";
                break;
        }
    }
}
