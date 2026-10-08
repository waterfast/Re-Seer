#include "lua_process.h"
#include <stdexcept>
#include <thread>
#ifdef _WIN32
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>
#else
#include <cerrno>
#include <csignal>
#include <poll.h>
#include <sys/wait.h>
#include <unistd.h>
#endif

namespace seer::battle {
using json = nlohmann::json;

LuaProcess::LuaProcess(const std::filesystem::path &executable, const std::filesystem::path &core) {
  try {
#ifdef _WIN32
    SECURITY_ATTRIBUTES security{sizeof(SECURITY_ATTRIBUTES), nullptr, TRUE};
    HANDLE childIn = nullptr, childOut = nullptr, parentIn = nullptr, parentOut = nullptr;
    if (!CreatePipe(&childIn, &parentIn, &security, 0)) throw std::runtime_error("Create input pipe failed");
    input_ = parentIn;
    if (!CreatePipe(&parentOut, &childOut, &security, 0)) {
      CloseHandle(childIn);
      throw std::runtime_error("Create output pipe failed");
    }
    output_ = parentOut;
    SetHandleInformation(parentIn, HANDLE_FLAG_INHERIT, 0);
    SetHandleInformation(parentOut, HANDLE_FLAG_INHERIT, 0);
    STARTUPINFOW startup{};
    startup.cb = sizeof(startup);
    startup.dwFlags = STARTF_USESTDHANDLES;
    startup.hStdInput = childIn;
    startup.hStdOutput = childOut;
    startup.hStdError = GetStdHandle(STD_ERROR_HANDLE);
    PROCESS_INFORMATION info{};
    std::wstring command = L"\"" + executable.wstring() + L"\" lua/server/rpc/entry.lua";
    BOOL ok = CreateProcessW(executable.c_str(), command.data(), nullptr, nullptr, TRUE,
                            CREATE_NO_WINDOW, nullptr, core.c_str(), &startup, &info);
    CloseHandle(childIn);
    CloseHandle(childOut);
    if (!ok) throw std::runtime_error("Cannot start Lua 5.4 process");
    CloseHandle(info.hThread);
    process_ = info.hProcess;
#else
    int in[2], out[2];
    if (pipe(in) != 0) throw std::runtime_error("pipe failed");
    if (pipe(out) != 0) { close(in[0]); close(in[1]); throw std::runtime_error("pipe failed"); }
    pid_ = fork();
    if (pid_ == 0) {
      dup2(in[0], STDIN_FILENO); dup2(out[1], STDOUT_FILENO);
      close(in[0]); close(in[1]); close(out[0]); close(out[1]);
      if (chdir(core.c_str()) != 0) _exit(126);
      execl(executable.c_str(), executable.c_str(), "lua/server/rpc/entry.lua", nullptr);
      _exit(127);
    }
    close(in[0]); close(out[1]);
    if (pid_ < 0) { close(in[1]); close(out[0]); throw std::runtime_error("fork failed"); }
    inputFd_ = in[1]; outputFd_ = out[0];
#endif
    auto hello = json::parse(readLine());
    if (hello.value("method", "") != "hello" || hello["params"].value("mode", "") != "json")
      throw std::runtime_error("Invalid Lua hello (JSON mode required)");
  } catch (...) { stop(); throw; }
}

LuaProcess::~LuaProcess() { stop(); }

void LuaProcess::stop() {
#ifdef _WIN32
  if (input_) { CloseHandle(input_); input_ = nullptr; }
  if (output_) { CloseHandle(output_); output_ = nullptr; }
  if (process_) {
    if (WaitForSingleObject(process_, 200) == WAIT_TIMEOUT) TerminateProcess(process_, 1);
    WaitForSingleObject(process_, 1000);
    CloseHandle(process_); process_ = nullptr;
  }
#else
  if (inputFd_ >= 0) { close(inputFd_); inputFd_ = -1; }
  if (outputFd_ >= 0) { close(outputFd_); outputFd_ = -1; }
  if (pid_ > 0) { kill(pid_, SIGTERM); waitpid(pid_, nullptr, 0); pid_ = -1; }
#endif
}

void LuaProcess::writeLine(const std::string &line) {
  std::string bytes = line + "\n";
  size_t sent = 0;
  while (sent < bytes.size()) {
#ifdef _WIN32
    DWORD count = 0;
    if (!WriteFile(input_, bytes.data() + sent, static_cast<DWORD>(bytes.size() - sent), &count, nullptr) || count == 0)
      throw std::runtime_error("Lua pipe write failed");
#else
    auto count = write(inputFd_, bytes.data() + sent, bytes.size() - sent);
    if (count < 0 && errno == EINTR) continue;
    if (count <= 0) throw std::runtime_error("Lua pipe write failed");
#endif
    sent += count;
  }
}

std::string LuaProcess::readLine() {
  const auto deadline = std::chrono::steady_clock::now() + std::chrono::seconds(5);
  for (;;) {
    auto newline = pending_.find('\n');
    if (newline != std::string::npos) {
      std::string line = pending_.substr(0, newline);
      pending_.erase(0, newline + 1);
      return line;
    }
    if (pending_.size() > 1024 * 1024) throw std::runtime_error("Lua message too large");
    if (std::chrono::steady_clock::now() >= deadline) throw std::runtime_error("Lua response timed out");
    char buffer[4096];
#ifdef _WIN32
    DWORD available = 0, count = 0;
    if (!PeekNamedPipe(output_, nullptr, 0, nullptr, &available, nullptr)) throw std::runtime_error("Lua exited");
    if (available == 0) { std::this_thread::sleep_for(std::chrono::milliseconds(2)); continue; }
    if (!ReadFile(output_, buffer, sizeof(buffer), &count, nullptr) || count == 0) throw std::runtime_error("Lua exited");
#else
    pollfd descriptor{outputFd_, POLLIN, 0};
    auto ready = poll(&descriptor, 1, 20);
    if (ready < 0 && errno == EINTR) continue;
    if (ready < 0) throw std::runtime_error("Lua pipe poll failed");
    if (ready == 0) continue;
    auto count = read(outputFd_, buffer, sizeof(buffer));
    if (count < 0 && errno == EINTR) continue;
    if (count <= 0) throw std::runtime_error("Lua exited");
#endif
    pending_.append(buffer, count);
  }
}

json LuaProcess::call(const std::string &method, const json &params) {
  int id = nextId_++;
  writeLine(json{{"jsonrpc", "2.0"}, {"id", id}, {"method", method}, {"params", params}}.dump());
  json events = json::array();
  // Notifications can arrive before a response. Always drain them while waiting.
  for (int messages = 0; messages < 1024; ++messages) {
    json packet = json::parse(readLine());
    if (packet.value("method", "") == "notifyPlayers") {
      for (const auto &event : packet["params"]["events"]) events.push_back(event);
    } else if (packet.contains("method") && packet.contains("id")) {
      writeLine(json{{"jsonrpc", "2.0"}, {"id", packet["id"]},
        {"error", {{"code", -32601}, {"message", "Unsupported parent method"}}}}.dump());
    } else if (packet.contains("id") && packet["id"] == id) {
      if (packet.contains("error")) return packet;
      auto result = packet["result"];
      result["events"] = events;
      packet["result"] = result;
      return packet;
    }
  }
  throw std::runtime_error("Too many Lua messages without a response");
}
}
