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


def test_admin_export_controls_are_above_dashboard_and_use_dedicated_classes():
    index = (ROOT / "frontend" / "index.html").read_text(encoding="utf-8")
    css = (ROOT / "frontend" / "css" / "style.css").read_text(encoding="utf-8")

    export_pos = index.index('class="card admin-export-card"')
    dashboard_pos = index.index('class="card admin-dashboard-card"')

    assert export_pos < dashboard_pos
    assert 'class="export-bar admin-export-bar"' in index
    assert 'class="btn btn-primary btn-sm export-action-btn"' in index
    assert ".admin-export-card" in css
    assert ".admin-export-bar" in css
    assert ".export-action-btn" in css


def test_header_stat_styles_are_declared_once():
    css = (ROOT / "frontend" / "css" / "style.css").read_text(encoding="utf-8")

    assert css.count(".stat-item{") == 1
    assert css.count(".stat-value{") == 1
    assert css.count(".stat-label{") == 1


def test_export_dialog_uses_shared_form_classes():
    admin_js = (ROOT / "frontend" / "js" / "admin.js").read_text(encoding="utf-8")
    css = (ROOT / "frontend" / "css" / "style.css").read_text(encoding="utf-8")

    for class_name in [
        "export-modal-field",
        "export-modal-label",
        "export-modal-select",
        "export-range-box",
        "export-range-row",
        "export-date-input",
    ]:
        assert class_name in admin_js
        assert f".{class_name}" in css


def test_static_asset_cache_version_is_current_and_consistent():
    index = (ROOT / "frontend" / "index.html").read_text(encoding="utf-8")
    app = (ROOT / "frontend" / "js" / "app.js").read_text(encoding="utf-8")
    sw = (ROOT / "frontend" / "sw.js").read_text(encoding="utf-8")

    assert "v=11" not in index + app + sw
    assert "v=12" not in index + app + sw
    assert "v=13" not in index + app + sw
    assert "ot-tracker-v11" not in sw
    assert "ot-tracker-v12" not in sw
    assert "ot-tracker-v13" not in sw
    assert 'ot-tracker-v14' in sw
    for asset in [
        "/css/style.css",
        "/js/utils.js",
        "/js/auth.js",
        "/js/stats.js",
        "/js/records.js",
        "/js/clock.js",
        "/js/admin.js",
        "/js/app.js",
        "/使用指南.html",
        "/管理员使用指南.html",
    ]:
        assert f"{asset}?v=14" in index + app + sw

