using System.IO.Compression;
using System.Text.Json;
using Qlvb.Infrastructure.Security;
using Qlvb.Infrastructure.Services;

namespace Qlvb.Tests;

/// <summary>Giai đoạn 4: nơi lưu dữ liệu, tệp sao lưu giả mạo hoặc bất thường.</summary>
public class HardeningTests
{
    [Fact]
    public void Location_TempFolder_Rejected()
    {
        Assert.NotNull(LocationPolicy.Reason(Path.Combine(Path.GetTempPath(), "QLVB", "Data")));
    }

    [Theory]
    [InlineData("OneDrive")]
    [InlineData("OneDrive - Cong ty ABC")]
    [InlineData("Dropbox")]
    [InlineData("Google Drive")]
    public void Location_CloudSyncFolder_Rejected(string cloud)
    {
        var root = OperatingSystem.IsWindows() ? @"D:\Users\canbo" : "/home/canbo";
        Assert.NotNull(LocationPolicy.Reason(Path.Combine(root, cloud, "QLVB", "saoluu.qlvbak")));
    }

    [Fact]
    public void Location_PublicFolder_Rejected()
    {
        var pub = Environment.GetEnvironmentVariable("PUBLIC");
        var old = pub;
        if (string.IsNullOrEmpty(pub))
        {
            pub = Path.Combine(Path.GetFullPath("/"), "chung-public");
            Environment.SetEnvironmentVariable("PUBLIC", pub);
        }
        try { Assert.NotNull(LocationPolicy.Reason(Path.Combine(pub, "Documents", "x.qlvbak"))); }
        finally { Environment.SetEnvironmentVariable("PUBLIC", old); }
    }

    [Fact]
    public void Location_PrivateFolder_Allowed_AndSimilarNamesNotConfused()
    {
        var root = OperatingSystem.IsWindows() ? @"D:\QLVB" : "/srv/QLVB";
        Assert.Null(LocationPolicy.Reason(Path.Combine(root, "Data")));
        Assert.Null(LocationPolicy.Reason(Path.Combine(root, "OneDriveCu", "Data"))); // chỉ trùng một phần tên
    }

    [Fact]
    public void Location_NetworkShare_WarnsOnWindows()
    {
        if (!OperatingSystem.IsWindows()) return;
        Assert.NotNull(LocationPolicy.Warning(@"\\maychu\chung\saoluu.qlvbak"));
    }

    [Fact]
    public void Backup_WithExtraEntry_Rejected()
    {
        using var env = new TestEnv();
        var file = env.Backup.Create(BackupKind.Nhanh);
        using (var zip = ZipFile.Open(file, ZipArchiveMode.Update))
        using (var w = new StreamWriter(zip.CreateEntry("../../ngoai.txt").Open())) w.Write("x");
        var ex = Assert.Throws<InvalidDataException>(() => BackupService.Inspect(file));
        Assert.Contains("cấu trúc", ex.Message);
    }

    [Fact]
    public void Backup_DeclaredSizeMismatch_Rejected()
    {
        using var env = new TestEnv();
        var file = env.Backup.Create(BackupKind.Nhanh);
        using (var zip = ZipFile.Open(file, ZipArchiveMode.Update))
        {
            var e = zip.GetEntry("manifest.json")!;
            BackupManifest m;
            using (var s = e.Open()) m = JsonSerializer.Deserialize<BackupManifest>(s)!;
            m.DatabaseSize += 4096;
            e.Delete();
            using var w = new StreamWriter(zip.CreateEntry("manifest.json").Open());
            w.Write(JsonSerializer.Serialize(m));
        }
        var ex = Assert.Throws<InvalidDataException>(() => env.Backup.Restore(file, TestEnv.Password));
        Assert.Contains("kích thước", ex.Message);
        Assert.NotNull(env.Store.GetConfig(Qlvb.Application.ConfigKeys.TenCoQuan)); // dữ liệu hiện tại còn nguyên
    }

    [Fact]
    public void Backup_HugeManifest_Rejected()
    {
        using var env = new TestEnv();
        var file = env.Backup.Create(BackupKind.Nhanh);
        using (var zip = ZipFile.Open(file, ZipArchiveMode.Update))
        {
            zip.GetEntry("manifest.json")!.Delete();
            using var w = new StreamWriter(zip.CreateEntry("manifest.json").Open());
            w.Write(new string(' ', 200_000));
        }
        Assert.Throws<InvalidDataException>(() => BackupService.Inspect(file));
    }

    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public void DataFolder_RestrictedToCurrentUser_OnWindows()
    {
        if (!OperatingSystem.IsWindows()) return;
        var dir = Directory.CreateTempSubdirectory("qlvb-acl-");
        try
        {
            File.WriteAllText(Path.Combine(dir.FullName, "a.txt"), "x");
            Assert.True(LocationPolicy.RestrictToCurrentUser(dir.FullName));
            var acl = System.IO.FileSystemAclExtensions.GetAccessControl(dir);
            Assert.True(acl.AreAccessRulesProtected);
            var sids = acl.GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier))
                .Cast<System.Security.AccessControl.FileSystemAccessRule>().Select(r => r.IdentityReference.Value).Distinct().ToList();
            var me = System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value;
            Assert.Contains(me, sids);
            Assert.DoesNotContain("S-1-5-11", sids); // Authenticated Users
            Assert.DoesNotContain("S-1-5-32-545", sids); // Users
            Assert.DoesNotContain("S-1-1-0", sids); // Everyone
        }
        finally { dir.Delete(true); }
    }
}
