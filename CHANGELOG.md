# Windows Share Manager v0.2.3

배포 ZIP에 .NET Desktop Runtime을 포함하지 않는 경량 배포 구조로 변경한 버전입니다. 기존 SMB 공유, 프린터 공유, 네트워크 진단과 권한 관리 기능은 그대로 유지합니다.

- 실제 WPF 앱을 .NET 10 Framework-dependent Single-file로 게시
- .NET 10 Desktop Runtime x64 설치 여부를 확인하는 Native AOT 런처 추가
- 런타임이 없으면 한국어 안내 후 Microsoft 공식 .NET 10 다운로드 페이지를 열 수 있도록 처리
- 런처는 일반 권한으로 실행하고 실제 관리 앱만 기존처럼 관리자 권한을 요청
- 배포 ZIP을 `WindowsShareManager.exe`와 `WindowsShareManager.App.exe` 두 파일로 단순화
- GitHub Actions의 자동 빌드, 테스트, ZIP, SHA-256, GitHub Release 생성 흐름 유지
