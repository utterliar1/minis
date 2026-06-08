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
