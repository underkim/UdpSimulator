# udp_sim — Linux C++ UDP Simulator CLI

DPDK 테스트 및 일반 UDP 통신 디버깅을 위한 인터랙티브 CLI 도구입니다.  
전체 기능 문서는 루트의 [README.md](../README.md)를 참고하세요.

## 빌드

```bash
cd udp_sim_cpp
cmake -B build -DCMAKE_BUILD_TYPE=Release
cmake --build build
```

실행 파일: `build/udp_sim`

## 빠른 시작

```bash
# 양방향 모드, 포트 5000
./build/udp_sim -l 0.0.0.0 -p 5000 -r 192.168.1.10 -q 5000

# 100ms 간격 자동 전송 + 즉시 pcap 캡처
./build/udp_sim -p 5000 -r 192.168.1.10 -q 5000 -d DEADBEEF -i 100 -c capture.pcap
```

## 주요 명령어 요약

```
send <hex>              hex 바이트 단발 전송
sendt <text>            ASCII 텍스트 전송
burst <n>               현재 payload n번 즉시 연속 전송
ping [timeout_ms]       RTT 측정 (payload 전송 후 첫 RX까지)
rate                    TX/RX pkts/sec 표시

auto <ms>               자동 전송 시작 (0=중지)
payload / counter / random payload 모드 설정

template save <name>    현재 payload를 이름으로 저장
template load <name>    저장된 payload 불러오기
template list           저장된 템플릿 목록

logoff / logon          실시간 출력 일시정지 / 재개
logtx [n] / logrx [n]  TX 또는 RX 패킷만 n개 표시
resend [n]              n번째 마지막 패킷 재전송

pcap <file>             pcap 캡처 시작
status / config / help  상태·설정 출력
```
