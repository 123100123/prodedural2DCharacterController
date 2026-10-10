"""Tiny client for the Unity MCP server (streamable HTTP, JSON-RPC) at 127.0.0.1:8080/mcp.

usage:
  python Tools/unity_mcp.py tools                          list tool names
  python Tools/unity_mcp.py call <tool> '<json args>'      call any tool
  python Tools/unity_mcp.py code '<C# method body>'        run C# in the Editor (execute_code)
  python Tools/unity_mcp.py compile                        request a script compile and print console errors
  python Tools/unity_mcp.py test <EditMode|PlayMode> [name ...]   run tests (names are full or partial) and wait
"""
import json, os, sys, time, urllib.request

URL = "http://127.0.0.1:8080/mcp"
HEADERS = {"Content-Type": "application/json", "Accept": "application/json, text/event-stream"}
GROUPS = ("scripting_ext", "testing")  # tool groups that are off by default and per session


def post(payload, session=None):
    headers = dict(HEADERS)
    if session:
        headers["Mcp-Session-Id"] = session
    req = urllib.request.Request(URL, json.dumps(payload).encode(), headers)
    with urllib.request.urlopen(req, timeout=900) as resp:
        body = resp.read().decode()
        sid = resp.headers.get("Mcp-Session-Id")
    for line in body.splitlines():
        if line.startswith("data:"):
            body = line[5:].strip()
            break
    return (json.loads(body) if body.strip() else {}), sid


def session():
    reply, sid = post({"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {
        "protocolVersion": "2025-03-26", "capabilities": {}, "clientInfo": {"name": "claude", "version": "1"}}})
    post({"jsonrpc": "2.0", "method": "notifications/initialized"}, sid)
    for group in GROUPS:
        post({"jsonrpc": "2.0", "id": 9, "method": "tools/call",
              "params": {"name": "manage_tools", "arguments": {"action": "activate", "group": group}}}, sid)
    return sid


def call(tool, arguments, sid=None, retries=int(os.environ.get("MCP_RETRIES", "20"))):
    """Calls a tool, waiting (3 s per retry; set MCP_RETRIES) while Unity is compiling, reloading or has no session."""
    sid = sid or session()
    for _ in range(retries):
        reply, _ = post({"jsonrpc": "2.0", "id": 2, "method": "tools/call", "params": {"name": tool, "arguments": arguments}}, sid)
        text = json.dumps(reply)
        if "no_unity_session" in text or "Unity session not available" in text or "No Unity instances" in text:
            time.sleep(3)
            continue
        break
    return reply


def text_of(reply):
    result = reply.get("result", reply)
    parts = [item.get("text", "") for item in result.get("content", [])] if isinstance(result, dict) else []
    return "\n".join(parts) if parts else json.dumps(reply, indent=1)[:4000]


def parse(reply):
    try:
        return json.loads(text_of(reply))
    except ValueError:
        return {}


if __name__ == "__main__":
    cmd = sys.argv[1]
    if cmd == "tools":
        sid = session()
        reply, _ = post({"jsonrpc": "2.0", "id": 3, "method": "tools/list"}, sid)
        for tool in reply["result"]["tools"]:
            print(tool["name"])
    elif cmd == "call":
        print(text_of(call(sys.argv[2], json.loads(sys.argv[3]) if len(sys.argv) > 3 else {})))
    elif cmd == "code":
        print(text_of(call("execute_code", {"action": "execute", "code": sys.argv[2]})))
    elif cmd == "compile":
        sid = session()
        print(text_of(call("refresh_unity", {"mode": "force", "scope": "all", "compile": "request", "wait_for_ready": True}, sid))[:600])
        print(text_of(call("read_console", {"action": "get", "types": ["error"], "count": 30, "format": "plain"}, sid))[:4000])
    elif cmd == "test":
        sid = session()
        names = sys.argv[3:] or None
        mode = sys.argv[2]
        args = {"mode": mode, "include_failed_tests": True, "init_timeout": 120000}
        if names:
            args["test_names"] = names
        started = parse(call("run_tests", args, sid))
        job = (started.get("data") or {}).get("job_id") or started.get("job_id")
        if not job:
            print(json.dumps(started)[:2000]); sys.exit(1)
        # Polled here, not with the server's wait_timeout: while it waits, the server brings the Unity window to the front
        # whenever a test runs over 3 s with Unity in the background (its "focus nudge"), which steals the user's focus.
        while True:
            state = parse(call("get_test_job", {"job_id": job, "include_failed_tests": True}, sid))
            data = state.get("data") or state
            if str(data.get("status", "")).lower() not in ("running", "pending", "queued", "initializing"):
                print(json.dumps(data, indent=1)[:6000])
                break
            time.sleep(3)
