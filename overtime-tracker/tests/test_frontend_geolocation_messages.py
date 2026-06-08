import subprocess
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def run_node(script):
    return subprocess.run(
        ["node", "-e", script],
        cwd=ROOT,
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )


def test_geolocation_errors_use_user_facing_chinese_messages():
    script = r"""
const fs = require('fs');
const vm = require('vm');

const sandbox = {
  console,
  location: { origin: 'http://127.0.0.1' },
  localStorage: { getItem() { return null; } },
  setTimeout() {},
  URL: { createObjectURL() { return 'blob:'; }, revokeObjectURL() {} },
  document: {
    getElementById() {
      return {
        textContent: '',
        innerHTML: '',
        classList: { add() {}, remove() {} },
      };
    },
    createElement() { return { click() {} }; },
  },
};
sandbox.window = sandbox;

vm.createContext(sandbox);
vm.runInContext(fs.readFileSync('frontend/js/utils.js', 'utf8'), sandbox);

const cases = [
  [{ code: 1, PERMISSION_DENIED: 1 }, '定位权限未开启，请在浏览器地址栏允许位置访问后重试'],
  [{ code: 2, POSITION_UNAVAILABLE: 2 }, '暂时无法获取当前位置，请检查网络或系统定位服务'],
  [{ code: 3, TIMEOUT: 3 }, '获取位置超时，请移到信号更好的位置后重试'],
  [{ message: 'User denied Geolocation' }, '定位权限未开启，请在浏览器地址栏允许位置访问后重试'],
  [{ message: 'Only secure origins are allowed' }, '当前页面不支持定位，请使用 localhost、HTTPS 或受信任的访问地址'],
];

for (const [input, expected] of cases) {
  const actual = sandbox.OT.geoErrorMessage(input);
  if (actual !== expected) {
    throw new Error(`Expected ${expected}, got ${actual}`);
  }
}
"""
    result = run_node(script)

    assert result.returncode == 0, result.stderr

