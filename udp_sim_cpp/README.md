# udp_sim — Linux C++ UDP Simulator

DPDK 테스트 및 일반 UDP 통신 디버깅을 위한 인터랙티브 CLI 도구입니다.

## 빌드

```bash
cd udp_sim_cpp
cmake -B build -DCMAKE_BUILD_TYPE=Release
cmake --build build
```

실행 파일: `build/udp_sim`

## 기본 사용법

```bash
# 기본 bidir (양방향) 모드
./build/udp_sim -l 0.0.0.0 -p 5000 -r 192.168.1.10 -q 5000

# 카운터 자동 전송 + 즉시 pcap 캡처 시작
./build/udp_sim -p 5000 -r 192.168.1.10 -q 5000 -i 100 -c capture.pcap
```

## CLI 옵션

| 옵션 | 기본값 | 설명 |
|------|--------|------|
| `-l`, `--local-ip`    | `0.0.0.0`   | 바인딩할 로컬 IP |
| `-p`, `--local-port`  | `12345`     | 로컬 포트 |
| `-r`, `--remote-ip`   | `127.0.0.1` | 원격 IP |
| `-q`, `--remote-port` | `12346`     | 원격 포트 |
| `-m`, `--mode`        | `bidir`     | `send` / `recv` / `bidir` |
| `-d`, `--data`        | —           | 초기 자동 전송 payload (hex 문자열) |
| `-i`, `--interval`    | `0` (off)   | 자동 전송 간격 (ms) |
| `-M`, `--mtu`         | `1500`      | MTU 바이트 |
| `-c`, `--capture`     | —           | 시작 시 pcap 파일 경로 |

## 인터랙티브 명령어

### 단발 전송

| 명령어 | 설명 |
|--------|------|
| `send <hex>`     | hex 바이트 전송  (e.g. `send DE AD BE EF`) |
| `sendt <text>`   | ASCII 텍스트 전송 |
| `sendf <path>`   | 파일 바이너리 전송 |

### 자동 전송

| 명령어 | 설명 |
|--------|------|
| `auto <ms>`          | `<ms>` ms 간격으로 자동 전송 시작. `auto 0`으로 중지 |
| `payload <hex>`      | 고정 payload 설정 (Fixed 모드) |
| `payloadt <text>`    | ASCII payload 설정 |
| `payloadr <n>`       | 랜덤 n 바이트 payload (매 전송마다 재생성) |
| `counter [offset]`   | Counter 모드: 4바이트 LE 시퀀스 번호를 `offset` 위치에 삽입 (기본 0) |
| `counter off`        | Counter 모드 해제 |

**Payload 모드 비교:**

| 모드 | 동작 |
|------|------|
| Fixed   | 매 전송마다 동일 payload |
| Counter | payload[offset..offset+3]에 1씩 증가하는 LE uint32 삽입 |
| Random  | 매 전송마다 payload 전체 재랜덤화 |

### 로그

| 명령어 | 설명 |
|--------|------|
| `log [n]`       | 마지막 n개 패킷 한 줄 요약 (기본 10) |
| `show [n]`      | 마지막 n개 패킷 hex+ASCII 상세 출력 |
| `dump [n]`      | n번째 마지막 패킷 상세 출력 |
| `verbose on`    | RX 수신 시 자동 hex+ASCII 출력 |
| `verbose off`   | verbose 해제 |
| `clear`         | 로그 초기화 |

### Pcap 캡처

| 명령어 | 설명 |
|--------|------|
| `pcap <file.pcap>` | 지정 파일로 캡처 시작 |
| `pcap off`         | 캡처 중지 |
| `pcap status`      | 캡처 상태 확인 |

캡처 파일은 **LINKTYPE_RAW (101)** 형식으로 저장됩니다.  
Wireshark에서 바로 열 수 있으며 IPv4/UDP 헤더가 자동으로 합성됩니다.

### 기타

| 명령어 | 설명 |
|--------|------|
| `status` | TX/RX 통계 + 현재 상태 출력 |
| `config` | 현재 설정 출력 |
| `help`   | 명령어 목록 출력 |
| `quit`   | 종료 |

## 사용 예시

```
# 1) 카운터 모드로 100ms 간격 자동 전송
> payload AABBCCDD 00000000   # offset 4에 카운터 넣을 공간 확보
> counter 4                   # offset 4에 seq# 삽입
> auto 100

# 2) 파일 전송
> sendf /tmp/testdata.bin

# 3) 실시간 캡처 시작
> pcap /tmp/test.pcap

# 4) RX 패킷 상세 보기
> verbose on
```

## pcap 포맷 상세

- Global header: magic `0xa1b2c3d4`, link type `101` (LINKTYPE_RAW)  
- 각 패킷 레코드에 가상 IPv4 헤더 + UDP 헤더를 합성하여 기록  
- TX 패킷: src = local, dst = remote  
- RX 패킷: src = sender, dst = local  
