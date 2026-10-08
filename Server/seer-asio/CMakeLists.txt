# SPDX-License-Identifier: GPL-3.0-or-later
#
# 构建脚本，结构对齐 freekill-asio 的 CMakeLists.txt。
#
# 默认用独立版 Asio；想换成 Boost.Asio（freekill-asio 用的）：
#   cmake -S . -B build -DSEER_USE_BOOST_ASIO=ON

cmake_minimum_required(VERSION 3.18)

project(seer-asio VERSION 0.1.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

if(NOT CMAKE_BUILD_TYPE)
  set(CMAKE_BUILD_TYPE Debug)
endif()

add_compile_options(-Wall -Wextra -Wpedantic)

option(SEER_USE_BOOST_ASIO "Use Boost.Asio instead of standalone Asio" OFF)

find_package(Threads REQUIRED)

# Stage-one battle bridge uses private, pinned dependencies prepared by the build tool.
set(SEER_DEPS_DIR "${CMAKE_CURRENT_SOURCE_DIR}/.deps" CACHE PATH "Local dependencies")
add_executable(seer-battle-server
  src/battle/main.cpp
  src/battle/lua_process.cpp
)
target_include_directories(seer-battle-server PRIVATE
  "${SEER_DEPS_DIR}/asio-asio-1-30-2/asio/include" "${SEER_DEPS_DIR}")
target_compile_definitions(seer-battle-server PRIVATE ASIO_STANDALONE)
target_link_libraries(seer-battle-server PRIVATE Threads::Threads)
if(WIN32)
  target_compile_definitions(seer-battle-server PRIVATE _WIN32_WINNT=0x0601)
  target_link_libraries(seer-battle-server PRIVATE ws2_32 mswsock)
endif()

# ---- 服务端：入口 + 网络层 + 业务层 ----
add_executable(seer-server EXCLUDE_FROM_ALL
  src/main.cpp
  src/network/server_socket.cpp
  src/network/client_socket.cpp
  src/server/chat_server.cpp
)
target_include_directories(seer-server PRIVATE src)
target_link_libraries(seer-server PRIVATE Threads::Threads)

# ---- 客户端 ----
add_executable(seer-client EXCLUDE_FROM_ALL
  src/client/main.cpp
  src/client/console_client.cpp
)
target_include_directories(seer-client PRIVATE src)
target_link_libraries(seer-client PRIVATE Threads::Threads)

foreach(tgt seer-server seer-client)
  if(SEER_USE_BOOST_ASIO)
    target_compile_definitions(${tgt} PRIVATE SEER_USE_BOOST_ASIO)
  endif()
endforeach()
