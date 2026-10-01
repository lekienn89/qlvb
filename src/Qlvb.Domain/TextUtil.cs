using System.Globalization;
using System.Text;

namespace Qlvb.Domain;

public static class TextUtil
{
    /// <summary>Chuẩn hóa Unicode NFC, gộp khoảng trắng, bỏ ký tự điều khiển. Trả về null nếu rỗng.</summary>
    public static string? Clean(string? s)
    {
        if (s == null) return null;
        var sb = new StringBuilder(s.Length);
        bool space = false;
        foreach (var ch in s.Normalize(NormalizationForm.FormC))
        {
            if (ch == '\n' || ch == '\r' || ch == '\t' || char.IsWhiteSpace(ch))
            {
                space = sb.Length > 0;
                continue;
            }
            if (char.IsControl(ch)) continue;
            if (space) { sb.Append(' '); space = false; }
            sb.Append(ch);
        }
        return sb.Length == 0 ? null : sb.ToString();
    }

    /// <summary>Giống Clean nhưng giữ xuống dòng (dùng cho trích yếu, ghi chú).</summary>
    public static string? CleanMultiline(string? s)
    {
        if (s == null) return null;
        var lines = s.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
            .Select(Clean).Where(l => l != null);
        var joined = string.Join("\n", lines);
        return joined.Length == 0 ? null : joined;
    }

    /// <summary>Khóa tìm kiếm: bỏ dấu tiếng Việt, chữ thường ("Quyết định" → "quyet dinh").</summary>
    public static string SearchKey(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var ch in d)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(ch switch { 'đ' => 'd', 'Đ' => 'd', _ => char.ToLowerInvariant(ch) });
        }
        return Clean(sb.ToString()) ?? "";
    }

    public static string[] SearchTerms(string? query) =>
        SearchKey(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    public static string FormatDate(DateOnly d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    public static string FormatDate(DateOnly? d) => d is { } x ? FormatDate(x) : "";
    public static string So2(int n) => n.ToString("00", CultureInfo.InvariantCulture);
}

public static class SoKyHieu
{
    /// <summary>
    /// Gợi ý số, ký hiệu văn bản đi theo mẫu cấu hình. Biến: {so}, {viet_tat}, {ky_hieu_co_quan}, {nam}.
    /// Mẫu mặc định (Nghị định 30/2020/NĐ-CP): "{so}/{viet_tat}-{ky_hieu_co_quan}"; công văn: "{so}/{ky_hieu_co_quan}".
    /// </summary>
    public static string GoiY(string mau, string mauCongVan, int so, string? vietTat, string? kyHieuCoQuan, int nam)
    {
        var tpl = string.IsNullOrWhiteSpace(vietTat) ? mauCongVan : mau;
        var s = tpl.Replace("{so}", TextUtil.So2(so))
            .Replace("{viet_tat}", vietTat?.Trim() ?? "")
            .Replace("{ky_hieu_co_quan}", kyHieuCoQuan?.Trim() ?? "")
            .Replace("{nam}", nam.ToString(CultureInfo.InvariantCulture));
        // Dọn dấu thừa khi thiếu thành phần
        while (s.Contains("--")) s = s.Replace("--", "-");
        s = s.Replace("/-", "/").TrimEnd('-', '/');
        return s;
    }
}
