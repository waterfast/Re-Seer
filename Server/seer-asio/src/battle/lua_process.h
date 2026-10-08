#pragma once

#include <chrono>
#include <filesystem>
#include <string>
#include <vector>
#include <json.hpp>

namespace seer::battle {
// One room owns one process. All calls are serialized by its connection worker.
class LuaProcess {
public:
  LuaProcess(const std::filesystem::path &executable, const std::filesystem::path &core);
  ~LuaProcess();
  LuaProcess(const LuaProcess &) = delete;
  LuaProcess &operator=(const LuaProcess &) = delete;
  nlohmann::json call(const std::string &method, const nlohmann::json &params);
private:
  void writeLine(const std::string &line);
  std::string readLine();
  void stop();
  void *process_ = nullptr;
  void *input_ = nullptr;
  void *output_ = nullptr;
  int pid_ = -1;
  int inputFd_ = -1;
  int outputFd_ = -1;
  int nextId_ = 1;
  std::string pending_;
};
}
