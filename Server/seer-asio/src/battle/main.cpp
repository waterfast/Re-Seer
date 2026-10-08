#include <asio.hpp>
#include <atomic>
#include <array>
#include <filesystem>
#include <iostream>
#include <thread>
#include "lua_process.h"
#ifndef _WIN32
#include <csignal>
#endif

namespace {
using json = nlohmann::json;
using tcp = asio::ip::tcp;
constexpr uint32_t MaxFrameBytes = 64 * 1024;
std::atomic<int> activeConnections{0};

std::string receive(tcp::socket &socket) {
  std::array<unsigned char, 4> header;
  asio::read(socket, asio::buffer(header));
  uint32_t size = (uint32_t(header[0]) << 24) | (uint32_t(header[1]) << 16) |
                  (uint32_t(header[2]) << 8) | header[3];
  if (size == 0 || size > MaxFrameBytes) throw std::runtime_error("Invalid frame size");
  std::string body(size, '\0');
  asio::read(socket, asio::buffer(body));
  return body;
}

void send(tcp::socket &socket, const json &packet) {
  std::string body = packet.dump();
  if (body.size() > MaxFrameBytes) throw std::runtime_error("Response too large");
  uint32_t size = static_cast<uint32_t>(body.size());
  std::array<unsigned char, 4> header{static_cast<unsigned char>(size >> 24),
    static_cast<unsigned char>(size >> 16), static_cast<unsigned char>(size >> 8), static_cast<unsigned char>(size)};
  std::array<asio::const_buffer, 2> buffers{asio::buffer(header), asio::buffer(body)};
  asio::write(socket, buffers);
}

json error(const json &id, int code, const std::string &message) {
  return {{"jsonrpc", "2.0"}, {"id", id}, {"error", {{"code", code}, {"message", message}}}};
}

// The Lua core reports seats. Translate only the presentation IDs at this boundary.
void normalizeEvents(json &result) {
  json normalized = json::array();
  for (const auto &raw : result["events"]) {
    std::string type = raw.value("type", "");
    int target = raw.value("target", raw.value("pet", 0));
    if (type == "Damage") {
      normalized.push_back({{"type", "Damage"}, {"targetPetId", "pet-" + std::to_string(target)},
        {"amount", raw.value("damage", 0)}, {"critical", raw.value("crit", false)}});
    } else if (type == "HpChanged" && raw.value("num", 0) > 0) {
      normalized.push_back({{"type", "Recover"}, {"targetPetId", "pet-" + std::to_string(target)},
        {"amount", raw.value("num", 0)}, {"critical", false}});
    }
  }
  result["events"] = normalized;
}

void serve(tcp::socket socket, std::filesystem::path lua, std::filesystem::path core, int roomId) {
  struct ConnectionCount { ~ConnectionCount() { --activeConnections; } } count;
  try {
    std::unique_ptr<seer::battle::LuaProcess> process;
    for (;;) {
      json request, id = nullptr;
      try { request = json::parse(receive(socket)); }
      catch (const json::parse_error &) { send(socket, error(nullptr, -32700, "Invalid JSON")); continue; }
      if (!request.is_object() || request.value("jsonrpc", json()) != "2.0" ||
          !request.contains("method") || !request["method"].is_string() ||
          !request.contains("id") || !request["id"].is_number_integer() || request["id"].get<int64_t>() <= 0) {
        send(socket, error(nullptr, -32600, "A request needs jsonrpc, method and a positive integer id")); continue;
      }
      id = request["id"];
      if (!request.contains("params") || !request["params"].is_object()) {
        send(socket, error(id, -32602, "params must be an object")); continue;
      }
      std::string method = request["method"];
      json params = request["params"];
      if (method == "session.hello") {
        send(socket, {{"jsonrpc", "2.0"}, {"id", id}, {"result", {{"protocolVersion", 1}, {"playerId", "1"}}}});
        continue;
      }
      if (method != "battle.start" && method != "battle.action" && method != "battle.snapshot") {
        send(socket, error(id, -32601, "Unknown method")); continue;
      }
      if (method == "battle.start" && process) { send(socket, error(id, -32000, "Battle already started")); continue; }
      if (method != "battle.start" && !process) { send(socket, error(id, -32000, "Start a battle first")); continue; }
      if (method == "battle.start") process = std::make_unique<seer::battle::LuaProcess>(lua, core);
      // Room and player ownership come exclusively from this server connection.
      params["roomId"] = roomId;
      params["playerId"] = 1;
      auto response = process->call(method == "battle.start" ? "clientStartGame" :
        method == "battle.action" ? "clientAction" : "clientSnapshot", params);
      response["id"] = id;
      if (response.contains("result")) {
        normalizeEvents(response["result"]);
        auto state = response["result"];
        send(socket, {{"jsonrpc", "2.0"}, {"method", "battle.update"}, {"params", state}});
        response["result"] = {{"accepted", true}, {"sequence", state["sequence"]}};
      }
      send(socket, response);
    }
  } catch (const std::exception &e) {
    std::cerr << "Room " << roomId << " closed: " << e.what() << '\n';
  }
}
}

int main(int argc, char **argv) {
  try {
#ifndef _WIN32
    signal(SIGPIPE, SIG_IGN);
#endif
    if (argc < 3) { std::cerr << "Usage: seer-battle-server <lua executable> <seer-core directory> [port]\n"; return 2; }
    auto lua = std::filesystem::absolute(argv[1]);
    auto core = std::filesystem::absolute(argv[2]);
    int port = argc > 3 ? std::stoi(argv[3]) : 9527;
    if (port < 1 || port > 65535) throw std::runtime_error("Invalid port");
    asio::io_context io;
    // First-stage demo is local: every connection controls player 1 against Lua AI.
    tcp::acceptor listener(io, {asio::ip::make_address("127.0.0.1"), static_cast<unsigned short>(port)});
    std::cout << "Battle server listening on 127.0.0.1:" << port << std::endl;
    int nextRoom = 0;
    for (;;) {
      tcp::socket socket(io);
      listener.accept(socket);
      if (activeConnections >= 32) { socket.close(); continue; }
      ++activeConnections;
      std::thread(serve, std::move(socket), lua, core, ++nextRoom).detach();
    }
  } catch (const std::exception &e) { std::cerr << e.what() << '\n'; return 1; }
}
