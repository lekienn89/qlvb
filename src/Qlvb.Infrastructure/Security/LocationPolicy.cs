using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Qlvb.Infrastructure.Security;

/// <summary>
/// Quy tắc nơi đặt dữ liệu, bản sao lưu và tệp xuất: không đặt trong thư mục tạm, thư mục dùng chung (Public)
/// hay thư mục được đồng bộ lên đám mây (OneDrive, Dropbox, Google Drive, iCloud).
/// </summary>
public static class LocationPolicy
{
    private static readonly string[] CloudFolderNames = ["OneDrive", "Dropbox", "Google Drive", "GoogleDrive", "iCloudDrive", "iCloud Drive"];

    /// <summary>Lý do không được dùng <paramref name="path"/> (thư mục hoặc tệp); null nếu được phép.</summary>
    public static string? Reason(string path)
    {
        string full;
        try { full = Path.GetFullPath(path); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return "Đường dẫn không hợp lệ."; }

        if (IsUnder(full, Path.GetTempPath()))
            return "Đây là thư mục tạm của Windows: tệp có thể bị dọn dẹp tự động hoặc người khác đọc được.";

        foreach (var pub in PublicFolders())
            if (IsUnder(full, pub))
                return "Đây là thư mục dùng chung (Public): mọi tài khoản trên máy đều đọc được.";

        foreach (var cloud in CloudFolders())
            if (IsUnder(full, cloud))
                return "Đây là thư mục được đồng bộ lên đám mây (OneDrive…): dữ liệu mật sẽ bị tải lên Internet.";
        var segments = full.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (segments.Any(s => CloudFolderNames.Any(c => s.Equals(c, StringComparison.OrdinalIgnoreCase) || s.StartsWith(c + " - ", StringComparison.OrdinalIgnoreCase))))
            return "Đường dẫn có vẻ nằm trong thư mục đồng bộ đám mây (OneDrive, Dropbox, Google Drive…): dữ liệu mật sẽ bị tải lên Internet.";
        return null;
    }

    /// <summary>Cảnh báo (không chặn) khi đặt bản sao lưu trên thư mục mạng.</summary>
    public static string? Warning(string path)
    {
        var full = Path.GetFullPath(path);
        if (full.StartsWith(@"\\", StringComparison.Ordinal))
            return "Đây là thư mục mạng dùng chung. Chỉ lưu khi thư mục này được phép lưu tài liệu mật và chỉ người có thẩm quyền truy cập được.";
        return null;
    }

    private static IEnumerable<string> PublicFolders()
    {
        if (Environment.GetEnvironmentVariable("PUBLIC") is { Length: > 0 } p) yield return p;
        foreach (var f in new[] { Environment.SpecialFolder.CommonDocuments, Environment.SpecialFolder.CommonDesktopDirectory, Environment.SpecialFolder.CommonMusic, Environment.SpecialFolder.CommonPictures, Environment.SpecialFolder.CommonVideos })
            if (Environment.GetFolderPath(f) is { Length: > 0 } d) yield return d;
    }

    private static IEnumerable<string> CloudFolders()
    {
        foreach (var v in new[] { "OneDrive", "OneDriveCommercial", "OneDriveConsumer" })
            if (Environment.GetEnvironmentVariable(v) is { Length: > 0 } d) yield return d;
    }

    internal static bool IsUnder(string full, string folder)
    {
        string f;
        try { f = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
        if (f.Length == 0) return false;
        return full.Equals(f, StringComparison.OrdinalIgnoreCase)
               || full.StartsWith(f + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Chỉ cho tài khoản Windows đang dùng (cùng SYSTEM và nhóm Administrators) truy cập thư mục dữ liệu; bỏ quyền kế thừa
    /// (ví dụ "Authenticated Users" trên ổ D:). Chỉ áp dụng trên ổ cố định của máy: ổ USB/ổ rời không phân quyền,
    /// để bản portable vẫn mở được khi cắm sang máy khác. Trả về true nếu đã áp dụng.
    /// </summary>
    public static bool RestrictToCurrentUser(string directory)
    {
        if (!OperatingSystem.IsWindows()) return false;
        try { return RestrictWindows(directory); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException or IOException or NotSupportedException) { return false; }
    }

    [SupportedOSPlatform("windows")]
    private static bool RestrictWindows(string directory)
    {
        var di = new DirectoryInfo(directory);
        if (new DriveInfo(di.Root.FullName).DriveType != DriveType.Fixed) return false;
        var user = WindowsIdentity.GetCurrent().User;
        if (user == null) return false;
        var acl = new DirectorySecurity();
        acl.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        const InheritanceFlags inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        foreach (var sid in new[] { user, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null) })
            acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        di.SetAccessControl(acl);
        return true;
    }
}
