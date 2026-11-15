# XP Customizer

[English README](README.md)

[Steam 창작마당](https://steamcommunity.com/sharedfiles/filedetails/?id=3839806573)

정착민의 경험치 요구량, 학습 속도, 스킬 성장 방식을 조정하는 RimWorld 모드입니다. 초기 설정은 바닐라 경험치·학습 동작을 유지하도록 설계되었으며 대부분의 기능을 개별적으로 켜고 끌 수 있습니다.

## 주요 기능

- **경험치 요구량:** 총 경험치, 레벨 구간별 분배, 스킬별 요구량, 레벨 20 예비 경험치.
- **학습 속도:** 의식(Consciousness)·전역 작업 속도(GWS)의 영향, 배율과 변화 곡선.
- **일일 학습 한도:** 경험치 포화 한도 및 의식 보정.
- **포화 후 학습:** 포화 이후 효율과 의식·정신이상 임계치 보정.
- **스킬 경험치 감소:** 레벨 1~20의 사용자 지정 감쇠량. 레벨 0은 0으로 고정됩니다.
- **바이오테크 적성:** 유전자 적성별 보너스·페널티 조정. 해당 기능에는 Biotech DLC가 필요합니다.
- **독서·스킬 트레이너:** 획득 경험치 조정.
- **프로필:** 생성, 이름 변경, 덮어쓰기, 삭제, 휴지통 복원. Long (×3)과 Very Long (×6) 프로필 기본 제공. 자동 적용되지 않습니다.

## 필요 사항과 지원 범위

- **이 저장소는 RimWorld 1.6용 빌드 소스**이며 Visual Studio 2022 및 .NET Framework 4.7.2를 사용합니다.
- 필수 모드: Harmony (`brrainz.harmony`). 바이오테크 DLC는 바이오테크 관련 기능에만 필요합니다.
- 창작마당 패키지에서 RimWorld **1.7 실행 사례**는 있지만, 이 저장소에 **1.7 전용 빌드 프로젝트는 없습니다.** 전체 1.7 호환성 검증도 완료되지 않았습니다.
- **소스 저장소는 설치용 모드가 아닙니다.** DLL이나 게임·Harmony 라이브러리는 포함하지 않습니다.

## 빌드 안내

[`XpCustomizer.sln`](XpCustomizer.sln)을 Visual Studio에서 열고 [BUILDING.md](BUILDING.md)를 참고하세요. 개인 PC의 경로를 담은 `.props` 파일은 GitHub에 올리지 마세요. 다른 모드와 공유하던 기존 패키징 스크립트는 의도적으로 제외했습니다.

## 확인된 문제

과거 창작마당 구독 환경에서 Long과 Very Long 프로필 적용이 `EXCEPTION_BEFORE_MUTATION_InvalidOperationException`으로 거부된 기록이 있습니다. 이후 다른 Debug 환경에서 적용 성공도 확인했지만, 신규 구독 환경의 문제가 해결됐다는 뜻은 아닙니다. [KNOWN_ISSUES.md](KNOWN_ISSUES.md)를 확인하세요.

## 프로젝트 정보

- 모드 ID: `zcn.xpcustomizer`
- 제작자: ZCN
- 소스 BuildId: `XPC-RW16-4CC-T04-B1-B1-PNL1-HF1-RU1-GTL1-FLR1-R1-20261115`
- 저장소 갱신 버전: `XPC-GH2-GTL1-FLR1-MIT-R1-20261115` (최신 프로젝트에서 변경된 C# 12개 반영)
- [변경 기록](CHANGELOG.md) · [GH2 검증 보고서](docs/GH2_UPDATE_VERIFICATION.md) · [GH1 이전 기록](docs/GH1_CLEANUP_REPORT.md)

**라이선스:** [MIT License](LICENSE) · Copyright (c) 2026 ZCN. ZCN이 권리를 보유하거나 재라이선스할 권한이 있는 자체 저작물에 적용되며, RimWorld·Harmony·제3자 저작물에는 각자의 라이선스가 적용됩니다.
