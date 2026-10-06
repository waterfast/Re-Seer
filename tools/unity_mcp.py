"""中文说明：通过本机已安装的 MCP 服务操作 Unity，不发送鼠标键盘输入。"""
import json
import pathlib
import sys
import urllib.request

ROOT = pathlib.Path(__file__).resolve().parents[1]
SESSION_FILE = ROOT / "Temp/unity-mcp-session.txt"
URL = "http://127.0.0.1:8080/mcp"

def request(method, params=None):
    """使用标准 MCP JSON-RPC 与事件流响应；会话文件仅保存本机连接标识。"""
    headers = {"Content-Type": "application/json", "Accept": "application/json, text/event-stream"}
    if SESSION_FILE.exists():
        headers["Mcp-Session-Id"] = SESSION_FILE.read_text().strip()
    payload = {"jsonrpc": "2.0", "id": 1, "method": method, "params": params or {}}
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
    with opener.open(urllib.request.Request(URL, data=json.dumps(payload).encode(), headers=headers), timeout=55) as response:
        text = response.read().decode("utf-8")
        if text.lstrip().startswith("{"):
            return json.loads(text)
        messages = [json.loads(line[5:].strip()) for line in text.splitlines() if line.startswith("data:")]
        return messages[-1] if messages else {}

if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    params = json.loads(sys.argv[2]) if len(sys.argv) > 2 else {}
    print(json.dumps(request(sys.argv[1], params), ensure_ascii=False))
