from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def test_docker_image_includes_guide_pages_in_static_root():
    dockerfile = (ROOT / "Dockerfile").read_text(encoding="utf-8")

    assert "使用指南.html" in dockerfile
    assert "管理员使用指南.html" in dockerfile
    assert "/app/frontend/" in dockerfile


def test_records_header_uses_dedicated_layout_classes():
    index = (ROOT / "frontend" / "index.html").read_text(encoding="utf-8")
    css = (ROOT / "frontend" / "css" / "style.css").read_text(encoding="utf-8")

    assert 'class="records-card-header"' in index
    assert 'class="btn btn-outline btn-sm record-export-btn"' in index
    assert ".records-card-header" in css
    assert ".record-export-btn" in css

