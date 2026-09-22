# 실물 통신 디버깅 — 2026-09-21

## 수정 결과 — 0.3.0

사용자의 nRF Connect RAW 스크린샷은 `15FFF0FF6DB6435F6E7F37A14F7B5EB551F1256336A9`입니다. Flags AD 구조가 없는 22바이트 광고이며, 원본 방식으로 변환한 ID D0BF의 PowerOn을 담고 있습니다.

원본 휴대폰에서는 RF 신호 앞에 Flags를 포함한 15바이트가 있지만, 이 PC에서는 12바이트였습니다. 원본 18바이트 payload 앞에 `FF F0 FF`를 추가하여 RF 신호의 위치와 바로 앞 3바이트를 휴대폰과 같게 만든 뒤 실물 반응을 확인했습니다.

| 실물 시험 | 사용자 확인 |
| --- | --- |
| ID 6C3C, 패딩 3바이트, PowerOn, 2000ms | 켜짐 |
| ID 6C3C, 패딩 3바이트, PowerOff, 2000ms | 꺼짐 |
| ID 6C3C, 패딩 3바이트, PowerOn, 200ms | 켜짐 |

이 결과에 따라 0.3.0은 `EncodeForWindows`를 통해 등록·트레이 조작·기본 CLI 송신 모두 같은 패딩을 적용하며, 송신 시간은 200ms로 설정했습니다. ID 6C3C를 사용자 설정에 추가하고 선택합니다. 기존 목록은 백업·보존합니다.

자동 검사: 원본 패킷 52개와 보정 패킷 52개, 실물 전원 패킷 고정값, 설정·메뉴 검사 통과. 별도의 정수 LFSR 구현에서도 원본 광고와 보정 광고의 해당 RF 비트열이 같음을 확인했습니다. 단순 offset 12 보정의 이전 실물 실패 이유까지 확정한 것은 아닙니다. 등록·풍량·타이머·회전·모드의 개별 실물 동작은 아직 확인하지 않았습니다.

아래는 수정 전 시험 기록입니다.

## 확인한 사실

- 사용자 확인: PC 앱은 실행되며 nRF Connect에서 PC의 BLE 광고가 보입니다. 등록·제어 시 선풍기는 반응하지 않습니다.
- Windows 송신 로그에서 Waiting → Started → Stopped, 오류 Success를 확인했습니다. 각 시험은 실제 Started 이후 1초간 지속했습니다.
- PC에서 휴대폰 원본 My LEZEN 광고를 수신했습니다. 사용자는 같은 시점에 원본 앱으로 선풍기가 정상적으로 켜지고 꺼짐을 확인했습니다.
- 휴대폰의 등록 코드는 `6C3C`입니다. 수신 패킷을 역변환해 확인했습니다.
- 휴대폰 광고는 ConnectableUndirected이며 AD 구조는 `02 01 02` Flags 다음 Manufacturer Data입니다. Company ID는 `FFF0`입니다.
- 휴대폰의 켜기 제조사 데이터: `6DB6435F6E7F37A14F7B5EBE5DF9266572F0`.
- 휴대폰의 끄기 제조사 데이터: `6DB6435F6E7F37A14F7B5FBE5DF92666C2C6`.
- C# 코드가 만든 ID `6C3C`, PowerOn, offset 15 패킷은 위 실제 수신값과 정확히 일치합니다.

## 반응이 없었던 시험

| ID | 패킷 위치 보정 | 시간 | 명령 |
| --- | ---: | ---: | --- |
| 9C2B | 12 | 각각 1000ms | Bind, PowerOn |
| 9C2B | 15 (원본) | 각각 1000ms | Bind, PowerOn |
| 6C3C | 12 | 1000ms | PowerOn |
| 6C3C | 15 (원본과 동일) | 1000ms | PowerOn |

위 결과만으로 원인을 확정할 수 없습니다. 특히 송신 API의 성공은 선풍기의 수신 성공을 뜻하지 않습니다. 현재 PC 설정 파일의 ID는 사용자의 후속 조작으로 바뀔 수 있으므로 자동으로 덮어쓰지 않았습니다.

## 수정 전 남았던 확인

PC 광고 전체 RAW 데이터와 원본 휴대폰 광고의 AD 구조를 비교해야 합니다. manufacturer payload만 같아도 그 앞의 추가 AD 구조가 다르면 RF297L 신호 변환 위치가 달라질 수 있습니다. 12 또는 15바이트 보정은 아직 확인되지 않은 가설이며 기본 동작을 변경하지 않았습니다.

진단용 소스에는 `app/data/debug.log`에 송신 상태·패킷·시간을 남기는 기능과 `--capture <파일.jsonl> <초>` 수신 기능을 추가했습니다. 실제 운영 중인 실행 파일은 아직 이 진단 버전으로 교체하지 않았습니다.

Android의 connectable 광고는 Flags 공간을 포함합니다. [AOSP 송신 코드](https://android.googlesource.com/platform/packages/modules/Bluetooth/+/refs/heads/main/framework/java/android/bluetooth/le/BluetoothLeAdvertiser.java). Windows Publisher는 Flags를 시스템 예약 항목으로 취급합니다. [Microsoft API 문서](https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.advertisement.bluetoothleadvertisementpublisher). 이 차이만으로 현재 PC의 실제 헤더 길이를 확정할 수는 없습니다.
