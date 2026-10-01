"""Chuyển tài liệu Markdown (docs/*.md, CHANGELOG.md) sang HTML tự chứa, xem được ngoại tuyến.

Dùng: python tools/build-docs.py [thư_mục_đích]   (mặc định artifacts/docs)
Cần gói "markdown" (Python-Markdown, giấy phép BSD) – chỉ dùng khi build, không đi kèm phần mềm.
"""
import html
import pathlib
import re
import sys

import markdown

ROOT = pathlib.Path(__file__).resolve().parent.parent
OUT = pathlib.Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / "artifacts" / "docs"

CSS = """
body{font-family:"Segoe UI",Arial,sans-serif;font-size:15px;line-height:1.55;color:#1f2328;background:#fff;max-width:960px;margin:0 auto;padding:24px 32px 64px}
h1{font-size:1.9em;border-bottom:2px solid #8b1a1a;padding-bottom:.3em;color:#8b1a1a}
h2{font-size:1.4em;border-bottom:1px solid #d0d7de;padding-bottom:.25em;margin-top:1.8em}
h3{font-size:1.15em;margin-top:1.4em}
table{border-collapse:collapse;margin:1em 0;width:100%}
th,td{border:1px solid #d0d7de;padding:6px 10px;vertical-align:top;text-align:left}
th{background:#f6f8fa}
code{font-family:Consolas,monospace;background:#f6f8fa;padding:1px 4px;border-radius:3px;font-size:.92em}
pre{background:#f6f8fa;padding:12px;border-radius:6px;overflow:auto}
pre code{padding:0;background:none}
blockquote{margin:1em 0;padding:.6em 1em;border-left:4px solid #c9a227;background:#fffbea}
hr{border:0;border-top:1px solid #d0d7de;margin:2em 0}
nav{font-size:.9em;margin-bottom:1em}
nav a{margin-right:1em}
a{color:#0b5cad}
@media print{nav{display:none}body{max-width:none;padding:0}}
"""

ORDER = ["HuongDanSuDung", "HuongDanQuanTri", "TaiLieuKyThuat"]
SOURCES = [ROOT / "docs" / f"{n}.md" for n in ORDER] + sorted(p for p in (ROOT / "docs").glob("*.md") if p.stem not in ORDER) + [ROOT / "CHANGELOG.md"]
TITLES = {}


def title_of(text, fallback):
    m = re.search(r"^#\s+(.+)$", text, re.M)
    return m.group(1).strip() if m else fallback


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    texts = {p: p.read_text(encoding="utf-8") for p in SOURCES if p.exists()}
    for p, t in texts.items():
        TITLES[p.stem] = title_of(t, p.stem)
    nav = "".join(f'<a href="{s}.html">{html.escape(t)}</a>' for s, t in TITLES.items())
    for p, text in texts.items():
        # Liên kết giữa các tài liệu: .md -> .html (bỏ tiền tố docs/ hoặc ../)
        text = re.sub(r"\]\((?:\.\./|docs/)?([A-Za-z0-9_-]+)\.md(#[^)]*)?\)", r"](\1.html\2)", text)
        body = markdown.markdown(text, extensions=["tables", "fenced_code", "sane_lists"], output_format="html")
        page = (
            '<!doctype html>\n<html lang="vi"><head><meta charset="utf-8">'
            '<meta name="viewport" content="width=device-width, initial-scale=1">'
            '<meta http-equiv="Content-Security-Policy" content="default-src \'none\'; style-src \'unsafe-inline\'">'
            f"<title>{html.escape(TITLES[p.stem])}</title><style>{CSS}</style></head>"
            f"<body><nav>{nav}</nav>\n{body}\n</body></html>\n"
        )
        (OUT / f"{p.stem}.html").write_text(page, encoding="utf-8")
        print(f"{p.name} -> {OUT / (p.stem + '.html')}")


if __name__ == "__main__":
    main()
