from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def test_lunch_settings_are_removed_from_ui_and_docs():
    files = [
        ROOT / "frontend" / "index.html",
        ROOT / "frontend" / "js" / "admin.js",
        ROOT / "frontend" / "js" / "stats.js",
        ROOT / "backend" / "app.py",
        ROOT / "README.md",
        ROOT / "使用指南.html",
        ROOT / "管理员使用指南.html",
    ]
    combined = "\n".join(path.read_text(encoding="utf-8") for path in files)

    forbidden = [
        "lunchStart",
        "lunchEnd",
        "午休",
        "标准工时",
        "总在岗时间 - 标准工时",
    ]
    for phrase in forbidden:
        assert phrase not in combined

