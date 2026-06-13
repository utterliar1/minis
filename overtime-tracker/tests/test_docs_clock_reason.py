from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def test_docs_say_only_clock_in_requires_a_reason():
    docs = [
        ROOT / "README.md",
        ROOT / "使用指南.html",
        ROOT / "管理员使用指南.html",
    ]
    combined = "\n".join(path.read_text(encoding="utf-8") for path in docs)

    forbidden = [
        "上班需填写事由",
        "每次打卡都需要填写事由",
    ]
    for phrase in forbidden:
        assert phrase not in combined
    assert "上班打卡需要填写事由" in combined
    assert "下班打卡不需要重复填写事由" in combined


def test_docs_describe_work_category_clock_out_review_and_export_columns():
    docs = [
        ROOT / "README.md",
        ROOT / "使用指南.html",
        ROOT / "管理员使用指南.html",
    ]
    required = [
        "工作类别",
        "下班说明",
        "位置不一致",
        "跨天",
        "实际工作持续到次日",
        "忘记下班打卡，当前补记",
        "复核标记",
        "姓名、日期、星期、上班、下班、类型、工作类别、事由、下班说明、范围外、实际位置、复核标记、工时",
    ]
    for path in docs:
        text = path.read_text(encoding="utf-8")
        for phrase in required:
            assert phrase in text, f"{path.name} missing {phrase}"
