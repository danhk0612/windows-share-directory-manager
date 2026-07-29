# Windows Share Manager

Windows 11 PC의 로컬 폴더와 프린터 공유를 내부 네트워크에서 관리하는 한국어 GUI 프로그램입니다. Windows 명령어나 PowerShell을 직접 사용할 필요가 없습니다.

## 지원 환경

- Windows 11 x64
- 관리자 권한
- 배포 ZIP 사용 시 별도 .NET 런타임 불필요

Windows 10과 ARM64는 초기 버전의 지원 대상이 아닙니다.

## 주요 기능

- Windows에 실제 등록된 일반 SMB 공유 조회
- 다른 Windows 도구에서 만든 일반 SMB 공유도 함께 조회
- `ADMIN$`, `IPC$`, 드라이브 관리 공유 등 시스템 공유 자동 제외
- 존재하는 로컬 폴더를 새 SMB 공유로 등록
- 모든 사용자 또는 특정 로컬 사용자·그룹 선택
- 공유 설명 및 계정별 권한 수정
- 여러 폴더 공유를 선택해 일괄 삭제(실제 폴더와 파일은 유지)
- 네트워크 경로 복사와 실제 폴더 열기
- 로컬 프린터 조회, 프린터 공유 설정·수정·해제와 네트워크 경로 복사
- 네트워크 프로필, IPv4, SMB 2/3, TCP 445, 방화벽 규칙과 관련 서비스 상세 진단
- 진단 항목별 설명·해결 팁 및 관련 Windows 설정 바로 열기
- 기준 네트워크 어댑터 선택 및 선택한 네트워크 기준 진단
- 안전한 일부 진단 항목의 사용자 확인 후 자동 수정
- `%LOCALAPPDATA%\WindowsShareManager\Logs`에 최근 로그 파일 10개 보관

## 실행 방법

1. [Releases](../../releases)에서 최신 `WindowsShareManager-v버전-win-x64.zip`을 받습니다.
2. ZIP 파일을 원하는 폴더에 풉니다.
3. `WindowsShareManager.exe`를 실행합니다.
4. Windows UAC 창이 나타나면 관리자 권한 실행을 허용합니다.

SMB 공유 생성·수정·삭제와 NTFS 권한 변경에는 관리자 권한이 필요합니다. UAC를 취소하면 프로그램은 실행되지 않습니다.

초기 배포본은 코드 서명 인증서로 서명되지 않았으므로 Windows SmartScreen 경고가 표시될 수 있습니다. GitHub Releases에서 받은 파일인지 확인하고, 함께 제공되는 SHA-256 파일로 무결성을 확인한 뒤 실행하세요.

## 권한 선택

Windows 네트워크 접근에는 SMB 공유 권한과 폴더의 NTFS 권한이 모두 적용됩니다. 실제 접근 권한은 두 설정 중 더 제한적인 결과가 됩니다.

| 화면 표시 | SMB 권한 | NTFS 권한 |
|---|---|---|
| 읽기 전용 | Read | ReadAndExecute |
| 읽기 및 쓰기 | Change | Modify |
| 모든 권한 | Full | FullControl |

새 공유의 기본값은 **읽기 전용**입니다. 프로그램은 선택한 계정의 명시적 허용 규칙만 추가·수정하며, 기존 ACL 전체를 지우거나 상속 설정을 바꾸지 않습니다. 기존 거부(Deny) 규칙은 표시하지만 초기 버전에서는 새로 만들거나 변경하지 않습니다. 메인 목록의 권한 열은 SMB 공유 권한을 기준으로 표시합니다.

### 모든 사용자와 특정 사용자

- **모든 사용자**: Windows의 잘 알려진 SID `S-1-1-0`을 현재 Windows 언어의 계정 이름으로 변환해 사용합니다.
- **특정 사용자 또는 그룹**: 현재 PC의 로컬 계정 목록에서 선택하거나 `PC이름\사용자`, `BUILTIN\Users`, 도메인 계정 등을 직접 입력할 수 있습니다.

`Everyone` 권한이 있다고 해서 암호 없는 네트워크 로그인이 자동 허용되는 것은 아닙니다. Windows의 암호로 보호된 공유 정책과 접속 계정의 자격 증명이 별도로 적용됩니다.

## 다른 PC에서 접속

파일 탐색기 주소 표시줄에 다음과 같이 입력합니다.

```text
\\PC이름\공유이름
```

프로그램 상단에서 PC 이름을 확인하고, 목록의 **네트워크 경로 복사** 버튼을 사용할 수 있습니다. Windows에 실제 등록된 일반 SMB 공유를 기준으로 하므로 이 프로그램이 아닌 파일 탐색기, 컴퓨터 관리 또는 PowerShell에서 만든 공유도 표시됩니다.

### 기준 네트워크 어댑터

메인 화면에서 공유에 사용할 실제 네트워크 어댑터를 선택합니다. 프로그램은 기본 게이트웨이, 물리 어댑터 여부와 인터페이스 메트릭을 기준으로 기본 어댑터를 자동 선택합니다.

VMware, Hyper-V, WSL, VPN 등의 가상 어댑터는 기본 목록에서 숨깁니다. 해당 네트워크를 실제 공유 경로로 사용하려면 **가상/VPN 어댑터 표시**를 선택한 뒤 직접 지정할 수 있습니다. 마지막 선택은 `%LOCALAPPDATA%\WindowsShareManager\settings.json`에 저장됩니다.

어댑터 선택은 진단과 대표 IPv4 표시 기준입니다. SMB 폴더 공유와 프린터 공유 자체를 특정 어댑터에만 바인딩하지는 않습니다.

접속되지 않으면 프로그램의 **상세 네트워크 진단**에서 선택한 어댑터를 기준으로 다음을 확인합니다.

- 네트워크 프로필이 가능한 경우 `Private`인지
- 활성 IPv4 주소가 있는지
- SMB 2/3 서버와 TCP 445 수신이 활성 상태인지
- `File and Printer Sharing` 방화벽 허용 규칙이 활성화되어 있는지
- `Network Discovery` 방화벽 규칙과 Function Discovery 서비스가 활성 상태인지
- `Server`(`LanmanServer`) 서비스가 실행 중인지
- 프린터 공유는 `Print Spooler` 서비스가 실행 중인지
- 접속하는 PC에 올바른 Windows 계정과 암호를 입력했는지

각 항목의 버튼으로 네트워크 설정, 서비스 관리, 고급 방화벽, Windows 기능 또는 프린터 설정을 열 수 있습니다.

다음 항목은 변경 내용을 확인한 뒤 프로그램에서 자동 수정할 수 있습니다.

- 신뢰할 수 있는 내부 네트워크의 공용 프로필을 개인으로 변경
- Server, Print Spooler와 Function Discovery 서비스 시작
- 선택 네트워크의 현재 프로필에 해당하는 Windows 기본 파일 공유·네트워크 검색 방화벽 규칙 활성화

서비스 시작 유형, SMB 2/3, IP 주소, 암호 보호 공유는 자동 변경하지 않습니다. Windows가 여러 어댑터를 같은 네트워크 프로필로 식별하면 공용·개인 변경이 함께 반영될 수 있습니다. 방화벽 규칙도 특정 어댑터가 아니라 같은 Windows 네트워크 프로필을 사용하는 연결 전체에 적용될 수 있습니다.

## 프린터 공유

**프린터 공유** 탭에는 이 PC에 설치된 로컬 프린터가 표시됩니다. 프린터를 선택해 공유 이름, 설명과 위치를 저장하거나 기존 네트워크 공유를 해제할 수 있습니다. 공유 중인 프린터의 접속 경로는 다음 형식입니다.

```text
\\PC이름\프린터공유이름
```

초기 버전은 프린터 공유 상태만 관리합니다. 프린터 장치, 드라이버, 포트, 인쇄 작업과 프린터 보안 ACL은 삭제하거나 변경하지 않습니다. 클라이언트 PC의 드라이버 설치 및 Point and Print 동작은 조직 정책과 Windows 보안 설정에 따라 제한될 수 있습니다.

## 공유 삭제 주의사항

공유 삭제는 Windows의 네트워크 공유 등록만 제거합니다.

**실제 폴더와 내부 파일은 삭제되지 않으며, NTFS 권한도 자동 제거하지 않습니다.**

`Ctrl` 또는 `Shift` 키로 여러 공유를 선택하면 한 번에 삭제할 수 있습니다. 항목별로 삭제를 시도하므로 일부가 실패해도 나머지 항목을 계속 처리하고 실패한 공유를 따로 안내합니다.

## 개발 및 빌드

필요 환경:

- Visual Studio 2022 이상(.NET 데스크톱 개발 워크로드)
- .NET 10 SDK
- Windows 11 x64

```powershell
dotnet restore WindowsShareManager.sln
dotnet build WindowsShareManager.sln --configuration Release
dotnet run --project tests/WindowsShareManager.Tests/WindowsShareManager.Tests.csproj --configuration Release
```

## 배포

관리자 PowerShell이 아닌 일반 PowerShell에서도 빌드할 수 있습니다.

```powershell
.\scripts\publish-win-x64.ps1
```

결과는 기본적으로 `artifacts\win-x64`에 생성됩니다. 배포 설정은 `win-x64`, self-contained, 단일 EXE, 디버그 심볼 제외입니다.

`main` 브랜치의 프로젝트 버전이 처음 게시되면 GitHub Actions가 빌드와 테스트를 수행하고 같은 버전의 GitHub Release를 자동 생성합니다. 버전의 단일 기준은 `WindowsShareManager.csproj`의 `Version` 값입니다.

## Windows 11 실기기 점검

실제 SMB·NTFS·UAC·다른 PC 접속은 [Windows 11 실기기 점검 체크리스트](docs/windows-11-test-checklist.md)를 기준으로 확인합니다.

## 구현 방식과 제한사항

- SMB 목록과 작업에는 Windows 11에 기본 포함된 Windows PowerShell 5.1의 `SmbShare` cmdlet을 숨김 프로세스로 사용합니다. 사용자 입력은 스크립트 문자열에 연결하지 않고 프로세스 환경변수로 전달합니다.
- 프린터 조회와 공유 설정에는 Windows의 `PrintManagement` cmdlet을 숨김 프로세스로 사용합니다. 프린터 자체를 삭제하는 명령은 사용하지 않습니다.
- 네트워크 어댑터 선택 정보만 사용자 환경설정으로 저장하며 공유 목록과 Windows 설정을 별도로 복제해 저장하지 않습니다.
- NTFS 권한은 .NET 파일 시스템 ACL API로 처리합니다.
- 공유 이름과 실제 경로 변경은 지원하지 않습니다. 기존 공유를 삭제한 뒤 새로 생성해야 합니다.
- 원격 PC, NAS, 시스템 공유, 네트워크 드라이브, FTP, WebDAV는 관리하지 않습니다.
- 원격 프린터, 프린터 설치·삭제, 드라이버·포트·인쇄 작업 및 프린터 보안 ACL 관리는 지원하지 않습니다.
- 사용자 계정 생성, 방화벽 자동 변경, 네트워크 프로필 자동 변경, 실제 폴더·파일 삭제 기능은 없습니다.
- 실제 SMB, NTFS, UAC 동작은 Windows 11 x64 환경에서 최종 확인해야 합니다.

## 라이선스

이 프로젝트는 [MIT License](LICENSE)로 배포됩니다.
