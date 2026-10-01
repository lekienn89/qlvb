using Qlvb.Application;
using Qlvb.Domain;
using Qlvb.Infrastructure;
using Qlvb.Infrastructure.Services;
using Serilog;

namespace Qlvb.App.Infrastructure;

/// <summary>Ngữ cảnh dùng chung của ứng dụng (một người dùng, một phiên).</summary>
public static class Ctx
{
    public static AppPaths Paths { get; set; } = null!;
    public static AppSession Session { get; set; } = null!;
    public static ILogger Log { get; set; } = Serilog.Core.Logger.None;

    public static IDataStore Store => Session.RequireStore();
    public static IClock Clock => Session.Clock;
    public static VanBanService VanBan => new(Store, Clock, Session.User);
    public static DanhMucService DanhMuc => new(Store);
    public static SoDangKyService So => new(Store, Clock);
    public static CauHinhService CauHinh => new(Store);
    public static TraCuuService TraCuu => new(Store);
    public static BackupService Backup => new(Session);
    public static ExportService Export => new(Store);
    public static DuLieuMauService DuLieuMau => new(Store, VanBan, Clock);

    /// <summary>Phát sinh khi dữ liệu thay đổi (để các trang làm mới).</summary>
    public static event Action? DataChanged;
    public static void NotifyChanged() => DataChanged?.Invoke();

    public static bool InKyHieuDoMat => CauHinh.GetBool(ConfigKeys.InKyHieuDoMat);
    public static string TenLoaiSo(LoaiSo l) => l == LoaiSo.Di ? "văn bản đi" : "văn bản đến";
}
