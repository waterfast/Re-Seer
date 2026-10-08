"""Real TCP -> C++ -> Lua acceptance tests; starts and reaps its own server."""
import json
from pathlib import Path
import socket
import struct
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]


def frame(message):
    body = json.dumps(message, ensure_ascii=False).encode("utf-8")
    return struct.pack("!I", len(body)) + body


def read_exact(sock, length):
    data = bytearray()
    while len(data) < length:
        chunk = sock.recv(length - len(data))
        if not chunk:
            raise EOFError("Server disconnected")
        data.extend(chunk)
    return data


def receive(sock):
    size = struct.unpack("!I", read_exact(sock, 4))[0]
    assert 0 < size <= 65536
    return json.loads(read_exact(sock, size))


def request(sock, method, params, request_id, fragmented=False):
    data = frame(dict(jsonrpc="2.0", id=request_id, method=method, params=params))
    if fragmented:
        for chunk in (data[:1], data[1:3], data[3:7], data[7:]):
            sock.sendall(chunk)
            time.sleep(0.003)
    else:
        sock.sendall(data)
    updates = []
    while True:
        packet = receive(sock)
        if packet.get("method") == "battle.update":
            updates.append(packet["params"])
        elif packet.get("id") == request_id:
            return packet, updates


def main():
    suffix = ".exe" if __import__("os").name == "nt" else ""
    with socket.socket() as probe:
        probe.bind(("127.0.0.1", 0))
        port = probe.getsockname()[1]
    with (ROOT / "build" / "battle-smoke-server.txt").open("w", encoding="utf-8") as log:
        server = subprocess.Popen([str(ROOT / "build" / ("seer-battle-server" + suffix)),
            str(ROOT / ".deps" / ("lua" + suffix)), str(ROOT / "packages" / "seer-core"), str(port)],
            stdout=log, stderr=log)
        try:
            deadline = time.monotonic() + 8
            while True:
                try:
                    sock = socket.create_connection(("127.0.0.1", port), timeout=3)
                    break
                except OSError:
                    if time.monotonic() > deadline:
                        raise
                    time.sleep(0.05)
            with sock:
                hello, _ = request(sock, "session.hello", {}, 1, fragmented=True)
                assert hello["result"]["protocolVersion"] == 1
                print("PASS split header/body and protocol handshake")
                # Two frames in a single write must produce two separate replies.
                sock.sendall(frame(dict(jsonrpc="2.0", id=2, method="session.hello", params={})) +
                             frame(dict(jsonrpc="2.0", id=3, method="unknown", params={})))
                assert receive(sock)["id"] == 2
                assert receive(sock)["error"]["code"] == -32601
                sock.sendall(struct.pack("!I", 1) + b"{")
                assert receive(sock)["error"]["code"] == -32700
                print("PASS coalesced frames and malformed JSON")
                started, updates = request(sock, "battle.start", {}, 4)
                assert started["result"]["accepted"] and len(updates) == 1
                state = updates[0]
                assert state["canAct"] and state["selfPet"]["currentHp"] == state["selfPet"]["maxHp"]
                assert state["skills"][0]["currentPp"] == 10 and state["fifthSkill"]["currentPp"] == 25
                print("PASS authoritative initial HP, PP and fifth skill snapshot")
                battle_id = state["battleId"]
                params = dict(battleId=battle_id, promptId=state["promptId"],
                    playerId=2, roomId=999,
                    action=dict(type="UseSkill", skillId="demo_spark", targetPetId="pet-2"))
                response, updates = request(sock, "battle.action", params, 5, fragmented=True)
                assert "error" not in response, response
                state = updates[0]
                assert state["fifthSkill"]["currentPp"] == 24
                assert state["enemyPet"]["currentHp"] < state["enemyPet"]["maxHp"]
                assert any(event["type"] == "Damage" and event["amount"] > 0 for event in state["events"])
                print("PASS Lua fifth skill settlement, damage and server-owned identity")
                replay, replay_updates = request(sock, "battle.action", params, 6)
                assert replay_updates[0]["sequence"] == state["sequence"]
                assert replay_updates[0]["events"] == []
                assert replay_updates[0]["fifthSkill"]["currentPp"] == 24
                params["action"]["skillId"] = "demo_thunder"
                rejected, _ = request(sock, "battle.action", params, 7)
                assert rejected["error"]["code"] == -32602
                print("PASS repeated action does not spend PP twice; changed replay rejected")
                bad = dict(battleId=battle_id, promptId=state["promptId"],
                    action=dict(type="UseSkill", skillId="demo_thunder", targetPetId="pet-1"))
                rejected, _ = request(sock, "battle.action", bad, 8)
                assert rejected["error"]["code"] == -32602
                rejected, _ = request(sock, "battle.action", dict(bad, battleId="wrong"), 9)
                assert rejected["error"]["code"] == -32602
                print("PASS illegal target and foreign battle rejected")
                for index in range(100):
                    if state["finished"]:
                        break
                    before = state
                    good = dict(battleId=battle_id, promptId=state["promptId"],
                        action=dict(type="UseSkill", skillId="demo_thunder", targetPetId="pet-2"))
                    response, updates = request(sock, "battle.action", good, 10 + index)
                    assert "error" not in response, response
                    state = updates[0]
                    assert state["sequence"] == before["sequence"] + 1
                    assert state["skills"][0]["currentPp"] <= before["skills"][0]["currentPp"]
                assert state["finished"] and state["winnerPlayerId"] in ("1", "2")
                assert not state["canAct"] and all(not skill["available"] for skill in state["skills"])
                snapshot, updates = request(sock, "battle.snapshot", dict(battleId=battle_id), 200)
                assert updates[0]["finished"] and updates[0]["events"] == []
                print("PASS complete battle, final winner, locked input and snapshot recovery")
            with socket.create_connection(("127.0.0.1", port), timeout=3) as second:
                response, updates = request(second, "battle.start", {}, 1)
                assert updates[0]["battleId"] != battle_id
                assert updates[0]["selfPet"]["currentHp"] == updates[0]["selfPet"]["maxHp"]
                second.sendall(struct.pack("!I", 65537))
                assert second.recv(1) == b""
                print("PASS independent rooms, fresh battle and oversized frame rejection")
        finally:
            server.terminate()
            server.wait(timeout=5)
    print("All battle bridge acceptance checks passed")


if __name__ == "__main__":
    main()
