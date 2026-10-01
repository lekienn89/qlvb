namespace Qlvb.Infrastructure;

/// <summary>
/// Thư mục dữ liệu, tách khỏi thư mục cài đặt.
/// Bản cài đặt: %LOCALAPPDATA%\QLVB\... ; bản portable (có tệp "portable.flag" cạnh QLVB.exe): .\Data\...
/// </summary>
public sealed class AppPaths
{
    public const string PortableFlag = "portable.flag";
    public const string DatabaseFileName = "qlvb.db";
    public const string KeyFileName = "qlvb.key";

    public string Root { get; }
    public bool Portable { get; }
    public string Database => Path.Combine(Root, "Database");
    public string Backup => Path.Combine(Root, "Backup");
    public string Logs => Path.Combine(Root, "Logs");
    public string Export => Path.Combine(Root, "Export");
    public string Config => Path.Combine(Root, "Config");
    public string DatabaseFile => Path.Combine(Database, DatabaseFileName);
    public string KeyFile => Path.Combine(Database, KeyFileName);

    public AppPaths(string root, bool portable)
    {
        Root = Path.GetFullPath(root);
        Portable = portable;
    }

    public static AppPaths Detect(string appDirectory)
    {
        if (File.Exists(Path.Combine(appDirectory, PortableFlag)))
            return new AppPaths(Path.Combine(appDirectory, "Data"), true);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(local)) local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        return new AppPaths(Path.Combine(local, "QLVB"), false);
    }

    public void EnsureCreated()
    {
        foreach (var d in new[] { Root, Database, Backup, Logs, Export, Config })
            Directory.CreateDirectory(d);
    }

    /// <summary>Cấm đặt dữ liệu trong thư mục tạm của hệ thống.</summary>
    public bool IsInTempFolder()
    {
        var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        return Root.StartsWith(temp + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
