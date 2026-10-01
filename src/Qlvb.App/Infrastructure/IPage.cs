namespace Qlvb.App.Infrastructure;

/// <summary>Trang trong cửa sổ chính, nhận các phím tắt chung.</summary>
public interface IPage
{
    string Title { get; }
    void OnShow();
    void NewItem() { }
    void FocusSearch() { }
    void Print() { }
    void Reload() => OnShow();
}
