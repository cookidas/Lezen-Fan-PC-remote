# 원본 패킷 검증 재현

이 검증기는 My LEZEN 1.3.8의 ARM64 코드를 Unicorn에서 실행합니다. 원본 CRC, whitening, 전체 패킷 생성 결과를 독립 구현과 비교하며 블루투스 신호를 보내지 않습니다. Dart 배열 생성·접근만 대신 처리합니다.

Python 환경에서 다음을 실행하세요. 검증에 사용한 라이브러리 버전은 Unicorn 2.1.4, pyelftools 0.33입니다.

```powershell
python -m pip install unicorn==2.1.4 pyelftools==0.33
python .\verify_original.py "D:\분석폴더\libapp.so"
```

원본 `split_config.arm64_v8a.apk`를 ZIP으로 열어 `lib/arm64-v8a/libapp.so`를 꺼내면 됩니다. 원본 APK·라이브러리·디스어셈블리 파일은 이 배포물에 포함하지 않았습니다.

지원하는 파일의 SHA256은 다음과 같습니다. 다른 파일이면 실행 전에 중단합니다.

```text
1160c2e8446f39a6ad28ae25156a512425c00bb2e2c2835b588dfd6e9897f074
```

각 함수의 임의 입력 100건과 4개 장치 ID × 13개 명령의 패킷 52건을 확인합니다. 성공하면 이 폴더의 `report.json`, `protocol-vectors.json`을 다시 생성합니다. 저장 위치를 바꾸려면 `--output-dir "D:\검증결과"`를 붙이세요. 불일치 시 오류와 함께 종료합니다.

동봉된 결과는 모두 통과했으며 원본 명령어 6,926,968개를 실행했습니다. 이 결과는 패킷 계산을 검증한 것이며 실물 선풍기의 수신·동작 확인은 별도로 필요합니다.
