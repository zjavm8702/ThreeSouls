# Three Souls

Unity **6000.5.10f1**, Android 프로젝트입니다. `Assets`, `Packages`, `ProjectSettings`를 같은 폴더로 내려받고 Unity Hub에서 엽니다. 오디오·원본 작업 파일은 Git LFS를 사용하므로 복제할 PC에 Git LFS를 설치하고 `git lfs pull`을 실행합니다.

## Android 출시 준비

Build Profiles의 **Android Google Play Release**는 AAB, IL2CPP, ARM64, 최소 API 26/타깃 API 36을 사용합니다. 회사명·고유 패키지명·Google Play Games/OAuth ID·출시 서명은 별도 설정이 필요합니다. 현재 설정을 실제 출시 완료 상태로 간주하지 않습니다.

## Addressables 빌드 후 자동 업로드

Addressables의 활성 콘텐츠 빌더는 **Three Souls Packed Build + GitHub Publish**입니다. Groups → Build → New Build에서 이 빌더로 빌드하거나 Android 플레이어 빌드를 하면 성공한 콘텐츠 빌드 뒤 자동 업로드가 시작됩니다.

- 대상 저장소: https://github.com/zjavm8702/AddressableAssetsData
- Android 로드 주소: https://zjavm8702.github.io/AddressableAssetsData/Android
- 로컬 출력: `Bundle/Android`
- GitHub 인증: 해당 PC의 Git credential manager. 토큰을 소스나 Unity 에셋에 저장하지 않습니다.
- 업로드 완료/실패: Unity Console와 `Library/AddressablesPublish/LastPublish.log`, 성공 커밋은 `LastPublish.json`.
- 오프라인/검증용으로 다른 Remote.LoadPath를 쓰는 프로필은 업로드하지 않습니다.

카탈로그·해시·번들을 하나의 커밋으로 푸시하며 기존 해시 이름 번들은 보존합니다. GitHub Pages 배포에는 푸시 후 시간이 필요합니다. 실패한 빌드는 게시하지 않고, 업로드 실패는 로컬 빌드 성공과 별도로 보고합니다. 이 설정은 **로컬 Unity 빌드 후 자동 업로드**이며 GitHub에 프로젝트를 푸시하는 것만으로 클라우드에서 Unity 빌드가 실행되는 설정은 아닙니다.

현재 출력 수동 검사:

```powershell
powershell -NoProfile -File scripts/Publish-Addressables.ps1 -SourceDirectory Bundle/Android -Target Android -ValidateOnly
```

수동 재게시하려면 마지막 `-ValidateOnly`를 빼고 실행합니다. 배포 파일은 LFS를 사용하지 않습니다. 번들 하나가 100 MiB 이상이면 번들을 작게 나누어야 합니다. 게시 작업은 파일 잠금으로 중복 업로드를 막고 원격 변경을 강제 덮어쓰지 않습니다.

`Library`, `Temp`, 빌드 출력, 로컬 검증 백업과 서명 키는 프로젝트 저장소에서 제외합니다. 편집기에서 미저장인 씬 변경은 Git에 포함되지 않습니다.
