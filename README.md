# LezenTray

Windows 시스템 트레이에서 동작하는 Bluetooth LE(BLE) 기반 선풍기/팬 제어 애플리케이션입니다.

## 주요 기능

- 시스템 트레이에서 등록된 BLE 기기(선풍기 등)를 검색 및 등록
- 전원 On/Off, 풍속 조절(Speed Up 등) 제어
- 여러 기기 중 사용할 기기 선택 및 관리
- 한국어(ko) 등 다국어 UI 지원
- 동작 로그 기록(디버그 로그)

> 위 기능 목록은 배포된 실행 파일 및 로그/설정 파일을 바탕으로 정리한 내용입니다. 실제 기능과 차이가 있다면 알려주세요.

## 실행 환경

- Windows 10/11
- [.NET 9.0](https://dotnet.microsoft.com/) 런타임
- Bluetooth LE를 지원하는 어댑터

## 설치 및 실행

1. [Releases](../../releases) 또는 배포된 빌드 파일을 다운로드합니다.
2. `LezenTray.exe`를 실행합니다.
3. 실행 시 시스템 트레이 아이콘이 생성되며, 트레이 메뉴를 통해 BLE 기기를 등록하고 제어할 수 있습니다.

## 트레이 메뉴

| 항목 | 동작 |
| --- | --- |
| 전원 | 클릭할 때마다 켜기/끄기. 켜기 전송 후 초록색, 끄기 전송 후 빨간색 |
| 바람 강도 | `＋ 바람 세게` / `－ 바람 약하게` |
| 타이머 | `＋ 시간 늘리기` / `－ 시간 줄이기` |
| 좌우 회전 | 클릭할 때마다 회전 켜기/끄기 |
| 바람 모드 | 일반풍 / 자연풍 / 수면풍 / 온도모드 |
| 선풍기 추가 | 장치 추가 모드, 저장된 선풍기 선택, 이름 변경·제거·등록 재전송 |

설정은 `app/data/fans.json`에 저장합니다.

## 설정 파일

`data/fans.json`에 등록된 기기 정보(ID, 이름)와 선택된 기기, 언어 설정이 저장됩니다.

## 선풍기 연결 문제

먼저 등록 → 전원 켜기/끄기 → 풍량 → 회전 → 모드 → 타이머 순서로 확인하면 됩니다. 반응이 없다면 블루투스가 켜졌는지 확인하고, 선풍기 가까이에서 전원 플러그를 다시 꽂은 뒤 저장된 장치의 `등록 신호 다시 보내기`를 사용하세요.

## 빌드 방법

```bash
git clone https://github.com/<사용자명>/LezenTray.git
cd LezenTray
dotnet build -c Release
```

## 소스와 검증

```powershell
dotnet publish ./src/LezenTray.csproj -c Release -o ./app
dotnet ./app/LezenTray.dll --self-test ./menu-preview.png
dotnet ./app/LezenTray.dll --adapter
```

진단용 `--send <네자리ID> <명령이름>`도 제공합니다. 이 옵션은 **실제 신호를 전송**하며 저장된 목록은 변경하지 않습니다. 예를 들어 `dotnet ./app/LezenTray.dll --send 1234 PowerOn`은 ID 1234에 켜기 명령을 보냅니다.

기본 `--send`도 트레이와 같은 Windows 보정 경로를 사용합니다. 원본 비교용으로만 `--send <ID> <명령> 15 200 raw`를 사용할 수 있습니다. `--capture <파일.jsonl> <초>`는 My LEZEN 광고를 수신·기록하는 진단 옵션입니다.
