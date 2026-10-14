# Three Souls

**기사·궁수·마법사, 세 영웅과 함께하는 Android 픽셀 판타지 RPG.**

마을에서 장비와 공급품을 준비하고, 던전을 탐험하며 전리품으로 성장하는 Unity 게임 개발 포트폴리오입니다.

[소개 페이지](https://zjavm8702.github.io/ThreeSouls/) · [APK 다운로드](https://github.com/zjavm8702/ThreeSouls/releases/download/portfolio-v1.0/ThreeSouls-Portfolio-1.0.apk) · [릴리스 / 설치 안내](https://github.com/zjavm8702/ThreeSouls/releases/tag/portfolio-v1.0) · [개발 과정](portfolio/DEVELOPMENT.md)

> Google Play 정식 출시본이 아닌 공개 체험판입니다. Android 8.0 이상 / ARM64에서 설치할 수 있습니다. 첫 실행에는 원격 콘텐츠 다운로드를 위한 인터넷 연결이 필요합니다. 공개 APK는 Google 로그인·클라우드 저장·실제 광고를 사용하지 않고 진행을 기기에 저장합니다.

## 게임에서 하는 일

- 기사·궁수·마법사 3인 파티의 자동 이동·기본 공격과 직업별 스킬
- 마을 NPC와 의뢰, 장비 상점, 던전 선택
- 영웅별 장비 비교·장착·강화와 스킬 룬 제작·강화
- 부대 공통 레벨·경험치와 영웅별 능력치 성장
- 회복 물약 편성, 자동 회복 기준, 전투 결과와 전리품
- 4개 던전 지역 / 20개 캠페인 스테이지 데이터

## 플레이 화면

개발·검증 중 촬영한 실제 화면입니다. 일부 문구와 배치는 공개 APK와 다를 수 있습니다.

| 마을 | 전투 | 인벤토리 |
| :---: | :---: | :---: |
| <img src="portfolio/screenshots/village.png" width="220" alt="마을과 의뢰"> | <img src="portfolio/screenshots/combat.png" width="220" alt="3인 전투"> | <img src="portfolio/screenshots/inventory.png" width="220" alt="영웅별 장비"> |

| 영웅 성장 | 출정 준비 | 던전 지도 |
| :---: | :---: | :---: |
| <img src="portfolio/screenshots/characters.png" width="220" alt="영웅 성장"> | <img src="portfolio/screenshots/supplies.png" width="220" alt="물약과 자동 회복"> | <img src="portfolio/screenshots/map.png" width="220" alt="던전 지역"> |

## 구현과 문제 해결

| 주제 | 구현 내용 |
| --- | --- |
| 전투와 성장 | 자동 이동·공격, 직업별 스킬, 장비 전리품, 공통 경험치와 영웅별 강화 |
| 데이터 | Google Sheets → CSV / JSON / ScriptableObject 가져오기, 문자열 키 생성 |
| 저장 | 로컬 저장·백업·복구, 저장 마이그레이션, 별도 빌드의 GPGS / Unity Cloud Save 연동 |
| 콘텐츠 배포 | 원격 Addressables, SpriteAtlas 수명 관리, 성공한 콘텐츠 빌드 뒤 GitHub Pages 자동 게시 |
| 모바일 UI | uGUI / TMP, 긴 화면과 안전 영역, 모달 입력 차단, Android Back 처리 |
| 빌드 | 서명 APK / AAB, IL2CPP ARM64, 원격 자산 앱 포함 방지 검사 |

문제의 원인, 수정 방식, 검증 범위는 [개발 기록](portfolio/DEVELOPMENT.md)에 정리했습니다. Google 로그인과 클라우드 백업은 기존 실기기 테스트에서 확인했지만 **공개 체험 APK에서는 비활성화**했습니다. 전체 20스테이지 완주와 모든 Android 기기 호환성을 검증했다고 주장하지 않습니다.

## APK 설치

1. Android 기기에서 [APK를 다운로드](https://github.com/zjavm8702/ThreeSouls/releases/download/portfolio-v1.0/ThreeSouls-Portfolio-1.0.apk)합니다.
2. 파일을 열고 해당 브라우저 / 파일 앱의 **알 수 없는 앱 설치**를 허용합니다.
3. 설치 후 실행하고 첫 콘텐츠 다운로드를 완료합니다.

앱 삭제·기기 변경 시 로컬 기록을 복구할 수 없습니다. 기존 Three Souls 설치본을 업데이트할 수 있으므로 중요한 기록은 먼저 기기 백업을 해 주세요. 자세한 안내와 SHA-256은 [릴리스](https://github.com/zjavm8702/ThreeSouls/releases/tag/portfolio-v1.0)에 있습니다.

## 개발 환경 / 소스 열기

Unity **6000.5.10f1**, C#, Android, IL2CPP ARM64, min API 26 / target API 36을 사용합니다. `Assets`, `Packages`, `ProjectSettings`를 같은 폴더로 내려받고 Unity Hub에서 엽니다. 오디오·원본 작업 파일은 Git LFS를 사용하므로 `git lfs pull`을 실행합니다.

공개 소스는 저장소에 커밋된 프로젝트 스냅샷이며, 체험 APK는 2026-10-14 로컬 작업본을 별도 빌드했습니다. 서명 키·비밀번호와 로컬 검증 로그는 공개하지 않습니다. 체험판 컴파일 정의는 `THREESOULS_PORTFOLIO`입니다.

## Addressables 배포

- 콘텐츠 저장소: https://github.com/zjavm8702/AddressableAssetsData
- Android 로드 주소: https://zjavm8702.github.io/AddressableAssetsData/Android
- 활성 빌더: **Three Souls Packed Build + GitHub Publish**
- 빌드 성공 뒤 카탈로그·해시·번들을 함께 게시하며 기존 해시 번들은 유지합니다.
- GitHub 인증은 해당 PC의 Git credential manager를 사용합니다.
- 프로젝트를 푸시하는 것만으로 Unity 빌드가 실행되는 설정은 아닙니다.

## 리소스와 피드백

이 포트폴리오는 게임 시스템·UI·데이터·빌드 흐름의 구현을 소개합니다. 외부 픽셀 아트·UI·폰트 리소스와 생성 이미지가 포함되어 있으며 모든 원화를 직접 제작했다는 의미는 아닙니다. 포함된 타사 리소스의 사용 조건은 각 제공자의 라이선스를 따릅니다.

버그와 의견은 [GitHub Issues](https://github.com/zjavm8702/ThreeSouls/issues)에 남겨 주세요.
