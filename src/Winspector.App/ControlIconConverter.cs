using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Winspector.App;

/// <summary>Bundled Google Material PNGs mapped to the provider's ControlType.</summary>
public sealed class ControlIconConverter : IValueConverter
{
    private static readonly Dictionary<string, BitmapImage> Images = [];
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var icon = (value as string) switch
        {
            "Window" or "TitleBar" => "web_asset",
            "Pane" or "Group" => "crop_square",
            "Custom" => "widgets",
            "Button" or "SplitButton" => "smart_button",
            "Edit" or "Document" or "Text" => "text_fields",
            "CheckBox" => "check_box",
            "RadioButton" => "radio_button_checked",
            "ComboBox" => "arrow_drop_down_circle",
            "List" or "ListItem" or "DataGrid" or "DataItem" or "Table" => "format_list_bulleted",
            "Tree" or "TreeItem" => "device_hub",
            "MenuBar" or "Menu" or "MenuItem" => "menu",
            "Tab" or "TabItem" => "tab",
            "Image" => "image",
            "ScrollBar" or "Slider" or "ProgressBar" or "Spinner" => "tune",
            _ => "help_outline"
        };
        // Conversion occurs on the WPF UI thread. Freeze images for safe reuse by templates.
        if (!Images.TryGetValue(icon, out var image))
        {
            image = new BitmapImage(new Uri($"pack://application:,,,/Winspector.App;component/Assets/MaterialIcons/{icon}.png"));
            image.Freeze();
            Images.Add(icon, image);
        }
        return image;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}