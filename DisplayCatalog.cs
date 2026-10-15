using System.Runtime.InteropServices;

namespace CodexLimitTray;

internal sealed record DisplayChoice(string Key, string Label, string DeviceName)
{
    public override string ToString() => Label;
}

internal static class DisplayCatalog
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public int Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
        public int Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Key;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplayDevices(string device, uint index, ref DisplayDevice data, uint flags);
    public static List<DisplayChoice> GetDisplays() => Screen.AllScreens.Select((screen, i) =>
    {
        var device = new DisplayDevice { Size = Marshal.SizeOf<DisplayDevice>() };
        var key = EnumDisplayDevices(screen.DeviceName, 0, ref device, 1) && !string.IsNullOrWhiteSpace(device.Id) ? device.Id : screen.DeviceName;
        var label = L10n.T("Ekran {0} · {1} × {2}", i + 1, screen.Bounds.Width, screen.Bounds.Height) + (screen.Primary ? " · " + L10n.T("Ana ekran") : "");
        return new DisplayChoice(key, label, screen.DeviceName);
    }).ToList();
    public static string? Resolve(string? key) => GetDisplays().FirstOrDefault(d => d.Key == key)?.DeviceName ?? Screen.PrimaryScreen?.DeviceName;
}
