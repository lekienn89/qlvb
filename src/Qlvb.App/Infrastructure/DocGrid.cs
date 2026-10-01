using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Qlvb.Domain;

namespace Qlvb.App.Infrastructure;

/// <summary>Dựng cột bảng danh sách theo biểu mẫu sổ (thay biểu mẫu → bảng tự thay theo, không hard-code).</summary>
public static class DocGrid
{
    private static readonly Dictionary<string, string> SortKeys = new()
    {
        [TruongBieuMau.SoThuTu] = "so_thu_tu", [TruongBieuMau.SoKyHieu] = "so_ky_hieu", [TruongBieuMau.NgayVanBan] = "ngay_van_ban",
        [TruongBieuMau.TenLoaiTrichYeu] = "ten_loai", [TruongBieuMau.DoMat] = "do_mat", [TruongBieuMau.NguoiKy] = "nguoi_ky",
        [TruongBieuMau.DonViLuu] = "don_vi_luu", [TruongBieuMau.NgayDen] = "ngay_den", [TruongBieuMau.SoDen] = "so_den",
        [TruongBieuMau.CoQuanBanHanh] = "co_quan_ban_hanh", [TruongBieuMau.DonViKyNhan] = "don_vi_nhan",
    };

    public static void Build(DataGrid grid, BieuMau bm, bool withStatus = true, bool withLoai = false)
    {
        grid.Columns.Clear();
        var wrap = (Style)System.Windows.Application.Current.FindResource("WrapCell");
        if (withLoai)
            grid.Columns.Add(new DataGridTextColumn { Header = "Sổ", Binding = new Binding("V.Loai") { Converter = LoaiConverter.Instance }, Width = 50, CanUserSort = false });
        for (var i = 0; i < bm.Cot.Count; i++)
        {
            var c = bm.Cot[i];
            var col = new DataGridTextColumn
            {
                Header = $"({c.So}) {c.TieuDe}",
                Binding = new Binding($"Cells[{i}]"),
                Width = new DataGridLength(c.DoRong, DataGridLengthUnitType.Star),
                MinWidth = 50,
                ElementStyle = wrap,
                CanUserSort = SortKeys.ContainsKey(c.Truong),
                SortMemberPath = SortKeys.GetValueOrDefault(c.Truong, ""),
            };
            grid.Columns.Add(col);
        }
        if (withStatus)
            grid.Columns.Add(new DataGridTextColumn { Header = "Trạng thái", Binding = new Binding(nameof(DocRow.TrangThai)), Width = 90, CanUserSort = false });

        var rowStyle = new Style(typeof(DataGridRow));
        var huy = new DataTrigger { Binding = new Binding(nameof(DocRow.DaHuy)), Value = true };
        huy.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x8A, 0x8F, 0x98))));
        huy.Setters.Add(new Setter(Control.FontStyleProperty, FontStyles.Italic));
        rowStyle.Triggers.Add(huy);
        var mau = new DataTrigger { Binding = new Binding(nameof(DocRow.LaMau)), Value = true };
        mau.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0xFF, 0xFB, 0xEB))));
        rowStyle.Triggers.Add(mau);
        grid.RowStyle = rowStyle;
    }

    private sealed class LoaiConverter : IValueConverter
    {
        public static readonly LoaiConverter Instance = new();
        public object Convert(object value, Type t, object p, System.Globalization.CultureInfo c) => value is LoaiSo.Di ? "Đi" : "Đến";
        public object ConvertBack(object value, Type t, object p, System.Globalization.CultureInfo c) => Binding.DoNothing;
    }
}
