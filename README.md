# UDP Simulator

UDP 통신 테스트·디버깅을 위한 도구입니다.  
**C# WPF GUI** (Windows)와 **C++ CLI** (Linux) 두 가지 버전을 제공합니다.

---

## 목차

- [C# WPF GUI](#c-wpf-gui)
- [C++ CLI](#c-cli)

---

## C# WPF GUI

### 빌드 / 실행

```
Visual Studio 2022 또는 .NET 8 SDK 필요
dotnet run --project UdpSimulator.csproj
```

---

### 화면 구성

```
┌─ Connection ──────────────────────────────────────────────────────────┐
│  Local IP / Port   Remote IP / Port   Mode   [Connect / Disconnect]   │
└───────────────────────────────────────────────────────────────────────┘
┌─ Send ────────────────────────────────────────────────────────────────┐
│  Burst: [N]  (N개 연속 전송)                                          │
│  Hex:  [입력창]                          [Send Hex]                   │
│  Text: [입력창]                          [Send Text]                  │
│  File: [경로]              [Browse…]     [Send File]                  │
└───────────────────────────────────────────────────────────────────────┘
┌─ Auto-send ───────────────────────────────┐ ┌─ Pcap Capture ─────────┐
│  Interval / Mode / Counter / Payload      │ │  [파일경로] [Browse]   │
│                         [Start Auto-send] │ │  [Start/Stop] [Export] │
└───────────────────────────────────────────┘ └────────────────────────┘
┌─ Payload Templates ───────────────────────────────────────────────────┐
│  Name: [입력]  [Save]   Load: [드롭다운]  [Load →]  [Delete]          │
└───────────────────────────────────────────────────────────────────────┘
┌─ Packet Log ──────────────────────────┐ ┌─ Hex Detail ───────────────┐
│  [All/TX/RX] [Scroll:ON] [Pause] [X] │ │  [Copy] [Resend]           │
│  Time  Dir  Peer  Bytes  Preview      │ │  0000  DE AD BE EF ...     │
└───────────────────────────────────────┘ └────────────────────────────┘
 Connected  0.0.0.0:12345 → 127.0.0.1:12346   RTT: 1.2ms   TX: 10 p/s   RX: 10 p/s   TX: 100 pkts ...
```

---

### 기능 상세

#### 연결 (Connection)

| 항목 | 설명 |
|------|------|
| Local IP / Port | 바인딩할 로컬 주소. 연결 중에는 변경 불가 |
| Remote IP / Port | 전송 대상 주소. 연결 중에도 변경 가능 |
| Mode | `Bidirectional` / `SendOnly` / `ReceiveOnly` |
| Connect / Disconnect | 클릭 한 번으로 소켓 바인딩 시작·종료 |

> 마지막으로 사용한 IP·Port·Mode는 앱 종료 시 자동 저장되어 다음 실행 시 복원됩니다.

---

#### 단발 전송 (Send)

| 항목 | 설명 |
|------|------|
| **Burst** | 한 번의 클릭으로 동일 패킷을 N번 연속 전송. 패킷 손실 테스트에 유용 |
| Send Hex | 공백·하이픈으로 구분된 hex 문자열 전송 (예: `DE AD BE EF`) |
| Send Text | UTF-8 텍스트를 바이트로 변환하여 전송 |
| Send File | 파일 전체를 바이너리로 전송 (MTU 초과 시 커널 단편화) |

---

#### 자동 전송 (Auto-send)

| 항목 | 설명 |
|------|------|
| Interval (ms) | 전송 간격 (밀리초) |
| Mode | `Fixed` / `Counter` / `Random` |
| Counter offset | Counter 모드에서 시퀀스 번호를 삽입할 바이트 위치 |
| Payload (hex) | 자동 전송에 사용할 기본 payload |

**Payload 모드:**

| 모드 | 동작 |
|------|------|
| Fixed | 매 전송마다 동일 payload |
| Counter | `offset` 위치에 4바이트 LE uint32 시퀀스 번호 자동 삽입 (0부터 증가) |
| Random | 매 전송마다 payload 전체를 랜덤 바이트로 재생성 |

---

#### Payload 템플릿 (Templates)

자주 사용하는 payload hex를 이름으로 저장·불러오기 할 수 있습니다.  
저장 위치: `%AppData%\UdpSimulator\templates.json`

| 버튼 | 동작 |
|------|------|
| Save | 현재 Auto-send Payload를 입력한 이름으로 저장 (같은 이름이면 덮어씀) |
| Load → | 선택한 템플릿을 Auto-send Payload 입력창에 로드 |
| Delete | 선택한 템플릿 삭제 |

---

#### Pcap 캡처

| 버튼 | 동작 |
|------|------|
| Browse… | 저장 경로 선택 |
| Start / Stop Capture | TX·RX 패킷을 pcap 파일로 저장 시작·중지 |
| Export Log | 현재 패킷 로그 전체를 pcap 파일로 내보내기 |

캡처 파일은 **LINKTYPE_RAW (101)** 형식으로, Wireshark에서 바로 열 수 있습니다.

---

#### 패킷 로그 (Packet Log)

| 컨트롤 | 설명 |
|--------|------|
| All / TxOnly / RxOnly | 방향 필터. 보고 싶은 방향만 표시 |
| Scroll: ON / OFF | 신규 패킷 수신 시 자동 스크롤 ON·OFF |
| Pause Log / Resume Log | 로그 업데이트 일시 정지·재개. 패킷은 계속 수신·계수됨 |
| Clear | 로그 전체 삭제 |

패킷 클릭 시 오른쪽 **Hex Detail** 패널에 hex+ASCII 상세 덤프 표시.

---

#### Hex Detail 패널

| 버튼 | 동작 |
|------|------|
| Copy | 현재 hex 덤프를 클립보드에 복사 |
| Resend | 선택한 패킷을 그대로 재전송 (Burst 횟수 적용) |

---

#### 상태 바 (Status Bar)

```
Connected 0.0.0.0:12345 → 127.0.0.1:12346   RTT: 1.2 ms   TX: 10 p/s   RX: 8 p/s   TX: 500 pkts / 2048 bytes   RX: 420 pkts / 1680 bytes
```

| 항목 | 설명 |
|------|------|
| 연결 상태 | 현재 로컬↔원격 주소 |
| RTT | 마지막 TX 직후 도착한 RX까지의 왕복 시간 |
| TX / RX p/s | 초당 패킷 수 (1초마다 갱신) |
| 누적 통계 | 총 TX·RX 패킷 수 및 바이트 수 |

---

### 설정 자동 저장

앱 종료 시 아래 항목이 자동 저장됩니다.  
저장 위치: `%AppData%\UdpSimulator\settings.json`

- Local IP / Port
- Remote IP / Port
- Mode
- Burst Count
- Auto-send Interval, Payload
- Send Hex 입력값

---

## C++ CLI

### 빌드

```bash
cd udp_sim_cpp
cmake -B build -DCMAKE_BUILD_TYPE=Release
cmake --build build
# 실행 파일: build/udp_sim
```

---

### 실행 옵션

```bash
./udp_sim [OPTIONS]
```

| 옵션 | 기본값 | 설명 |
|------|--------|------|
| `-l`, `--local-ip <ip>` | `0.0.0.0` | 바인딩 로컬 IP |
| `-p`, `--local-port <port>` | `12345` | 로컬 포트 |
| `-r`, `--remote-ip <ip>` | `127.0.0.1` | 원격 IP |
| `-q`, `--remote-port <port>` | `12346` | 원격 포트 |
| `-m`, `--mode <mode>` | `bidir` | `send` / `recv` / `bidir` |
| `-d`, `--data <hex>` | — | 초기 자동 전송 payload (hex) |
| `-i`, `--interval <ms>` | `0` (off) | 자동 전송 간격 (ms) |
| `-M`, `--mtu <bytes>` | `1500` | MTU |
| `-c`, `--capture <file>` | — | 시작 즉시 pcap 캡처 시작 |

**예시:**
```bash
# 양방향, 포트 5000
./udp_sim -l 0.0.0.0 -p 5000 -r 192.168.1.10 -q 5000

# 100ms 간격 자동 전송 + 즉시 캡처
./udp_sim -p 5000 -r 192.168.1.10 -q 5000 -d DEADBEEF -i 100 -c cap.pcap
```

---

### 인터랙티브 명령어

#### 단발 전송

| 명령어 | 설명 |
|--------|------|
| `send <hex>` | hex 바이트 전송 (예: `send DE AD BE EF`) |
| `sendt <text>` | ASCII 텍스트 전송 |
| `sendf <path>` | 파일 바이너리 전송 |
| `burst <n>` | 현재 auto-send payload를 즉시 n번 연속 전송 |

#### 자동 전송

| 명령어 | 설명 |
|--------|------|
| `auto <ms>` | ms 간격으로 자동 전송 시작. `auto 0`으로 중지 |
| `payload <hex>` | 고정 payload 설정 |
| `payloadt <text>` | ASCII payload 설정 |
| `payloadr <n>` | 랜덤 n 바이트 payload (매 전송마다 재생성) |
| `counter [offset]` | Counter 모드 활성화. offset 위치에 4바이트 LE seq# 삽입 |
| `counter off` | Counter 모드 해제 |

#### 테스트 편의 기능

| 명령어 | 설명 |
|--------|------|
| `burst <n>` | 현재 payload를 n번 즉시 연속 전송 |
| `ping [timeout_ms]` | 현재 payload 1회 전송 후 첫 RX 수신까지 RTT 측정 (기본 2000ms) |
| `rate` | 마지막 `rate` 호출 이후의 TX/RX pkts/sec 표시 |
| `resend [n]` | 로그의 n번째 마지막 패킷을 재전송 (기본 1) |

#### Payload 템플릿

자주 쓰는 payload를 이름으로 저장합니다.  
저장 위치: `~/.udpsim_templates/<name>.bin`

| 명령어 | 설명 |
|--------|------|
| `template save <name>` | 현재 payload를 이름으로 저장 |
| `template load <name>` | 저장된 payload를 auto-send payload로 로드 |
| `template list` | 저장된 템플릿 목록 출력 |
| `template del <name>` | 템플릿 삭제 |

**예시:**
```
> payload AA BB CC DD 01 02 03 04
> template save heartbeat
> template list
  heartbeat (8 bytes)
> template load heartbeat
```

#### 로그

| 명령어 | 설명 |
|--------|------|
| `log [n]` | 마지막 n개 패킷 한 줄 요약 (기본 10) |
| `logtx [n]` | 마지막 n개 TX 패킷만 표시 |
| `logrx [n]` | 마지막 n개 RX 패킷만 표시 |
| `show [n]` | 마지막 n개 패킷 hex+ASCII 상세 출력 |
| `dump [n]` | n번째 마지막 패킷 상세 출력 |
| `verbose [on\|off]` | RX 수신 시 자동 hex+ASCII 덤프 ON/OFF |
| `logon` | 실시간 RX/TX 출력 재개 |
| `logoff` | 실시간 출력 일시 정지 (수신·계수·pcap은 계속됨) |
| `logmax <n>` | 로그 버퍼 최대 크기 변경 (기본 1000) |
| `clear` | 로그 초기화 |

#### Pcap 캡처

| 명령어 | 설명 |
|--------|------|
| `pcap <file.pcap>` | 지정 파일로 캡처 시작 |
| `pcap off` | 캡처 중지 |
| `pcap status` | 캡처 상태 확인 |

캡처 파일은 **LINKTYPE_RAW (101)** 형식이며 Wireshark에서 바로 열 수 있습니다.

#### 정보

| 명령어 | 설명 |
|--------|------|
| `status` | TX/RX 통계, 연결 상태, auto-send 상태 출력 |
| `config` | 현재 IP·포트·MTU 설정 출력 |
| `help` | 전체 명령어 목록 출력 |
| `quit` / `q` | 종료 |

---

### 사용 예시

```
# 1. 서버처럼 RX 대기, 수신 패킷 즉시 상세 출력
> verbose on

# 2. 연결 품질 확인 (RTT 측정)
> payload 00 01 02 03
> ping 1000
RTT: 0.8 ms

# 3. 부하 테스트: 1000개 패킷 즉시 전송
> payload FF EE DD CC
> burst 1000

# 4. 100ms 간격 카운터 자동 전송
> payload 48 42 00 00 00 00 00 00
> counter 4
> auto 100

# 5. 특정 패킷 재현
> log 5          ← 최근 5개 확인
> dump 2         ← 2번째 마지막 패킷 상세 보기
> resend 2       ← 그 패킷 재전송

# 6. 실시간 속도 확인
> rate
TX: 10.0 pkts/s   RX: 9.8 pkts/s

# 7. 로그가 너무 많이 올라올 때
> logoff          ← 화면 출력 멈춤 (수신은 계속)
> logrx 20        ← 최근 RX 20개만 확인
> logon           ← 다시 실시간 출력
```
