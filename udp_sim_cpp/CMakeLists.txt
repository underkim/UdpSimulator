cmake_minimum_required(VERSION 3.16)
project(udp_sim CXX)

set(CMAKE_CXX_STANDARD          17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_executable(udp_sim
    main.cpp
    udp_sim.cpp
)

target_compile_options(udp_sim PRIVATE -Wall -Wextra -O2)
target_link_libraries(udp_sim  PRIVATE pthread)
