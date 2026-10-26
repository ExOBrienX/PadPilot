using Avalonia.Media;
using Avalonia.Media.Immutable;
using PadPilot.App.Services;
using PadPilot.Core.Models;

namespace PadPilot.App.ViewModels;

public sealed class ColorPreset
{
    public ColorPreset(string name, Rgb color)
    {
        Name = name;
        Color = color;
        Brush = new ImmutableSolidColorBrush(Avalonia.Media.Color.FromRgb(color.R, color.G, color.B));
    }

    public string Name { get; }

    public Rgb Color { get; }

    public IBrush Brush { get; }

    public static IReadOnlyList<ColorPreset> All { get; } =
    [
        new(Loc.T("color.blue"), new Rgb(0, 64, 255)),
        new(Loc.T("color.cyan"), new Rgb(0, 220, 255)),
        new(Loc.T("color.green"), new Rgb(0, 255, 60)),
        new(Loc.T("color.yellow"), new Rgb(255, 200, 0)),
        new(Loc.T("color.orange"), new Rgb(255, 90, 0)),
        new(Loc.T("color.red"), new Rgb(255, 0, 0)),
        new(Loc.T("color.pink"), new Rgb(255, 0, 170)),
        new(Loc.T("color.purple"), new Rgb(140, 0, 255)),
        new(Loc.T("color.white"), new Rgb(255, 255, 255)),
    ];

    public static ColorPreset? Find(Rgb color) => All.FirstOrDefault(p => p.Color == color);
}
