# Windows Share Manager

Windows 11 PC의 로컬 폴더를 내부 네트워크에 공유하고, 기존 SMB 공유와 권한을 조회·수정·삭제하는 한국어 GUI 프로그램입니다. Windows 명령어나 PowerShell을 직접 사용할 필요가 없습니다.

## 지원 환경

- Windows 11 x64
- 관리자 권한
- 배포 ZIP 사용 시 별도 .NET 런타임 불필요

Windows 10과 ARM64는 초기 버전의 지원 대상이 아닙니다.

## 주요 기능

- Windows에 실제 등록된 일반 SMB 공유 조회
- `ADMIN$`, `IPC$`, 드라이브 관리 공유 등 시스템 공유 자동 제외
- 존재하는 로컬 폴더를 새 SMB 공유로 등록
- 모든 사용자 또는 특정 로컬 사용자·그룹 선택
- 공유 설명 및 계정별 권한 수정
- SMB 공유 삭제(실제 폴더와 파일은 유지)
- 네트워크 경로 복사와 실제 폴더 열기
- 네트워크 프로필, 파일 및 프린터 공유 방화벽 규칙, Server 서비스 상태 진단
- `%LOCALAPPDATA%\WindowsShareManager\Logs`에 최근 로그 파일 10개 보관

## 실행 방법

1. [Releases](../../releases)에서 `WindowsShareManager-v0.1.0-win-x64.zip`을 받습니다.
2. ZIP 파일을 원하는 폴더에 풉니다.
3. `WindowsShareManager.exe`를 실행합니다.
4. Windows UAC 창이 나타나면 관리자 권한 실행을 허용합니다.

SMB 공유 생성·수정·삭제와 NTFS 권한 변경에는 관리자 권한이 필요합니다. UAC를 취소하면 프로그램은 실행되지 않습니다.

## 권한 선택

Windows 네트워크 접근에는 SMB 공유 권한과 폴더의 NTFS 권한이 모두 적용됩니다. 실제 접근 권한은 두 설정 중 더 제한적인 결과가 됩니다.

| 화면 표시 | SMB 권한 | NTFS 권한 |
|---|---|---|
| 읽기 전용 | Read | ReadAndExecute |
| 읽기 및 쓰기 | Change | Modify |
| 모든 권한 | Full | FullControl |

새 공유의 기본값은 **읽기 전용**입니다. 프로그램은 선택한 계정의 명시적 허용 규칙만 추가·수정하며, 기존 ACL 전체를 지우거나 상속 설정을 바꾸지 않습니다. 기존 거부(Deny) 규칙은 표시하지만 초기 버전에서는 새로 만들거나 변경하지 않습니다.

### 모든 사용자와 특정 사용자

- **모든 사용자**: Windows의 잘 알려진 SID `S-1-1-0`을 현재 Windows 언어의 계정 이름으로 변환해 사용합니다.
- **특정 사용자 또는 그룹**: 현재 PC의 로컬 계정 목록에서 선택하거나 `PC이름\사용자`, `BUILTIN\Users`, 도메인 계정 등을 직접 입력할 수 있습니다.

`Everyone` 권한이 있다고 해서 암호 없는 네트워크 로그인이 자동 허용되는 것은 아닙니다. Windows의 암호로 보호된 공유 정책과 접속 계정의 자격 증명이 별도로 적용됩니다.

## 다른 PC에서 접속

파일 탐색기 주소 표시줄에 다음과 같이 입력합니다.

```text
\\PC이름\공유이름
```

프로그램 상단에서 PC 이름을 확인하고, 목록의 **네트워크 경로 복사** 버튼을 사용할 수 있습니다.

접속되지 않으면 프로그램의 **네트워크 진단**에서 다음을 확인합니다.

- 네트워크 프로필이 가능한 경우 `Private`인지
- `File and Printer Sharing` 방화벽 허용 규칙이 활성화되어 있는지
- `Server`(`LanmanServer`) 서비스가 실행 중인지
- 접속하는 PC에 올바른 Windows 계정과 암호를 입력했는지

프로그램은 방화벽과 네트워크 프로필을 자동으로 변경하지 않습니다. Windows 설정의 **네트워크 및 인터넷 > 고급 네트워크 설정 > 고급 공유 설정**에서 직접 확인하세요.

## 공유 삭제 주의사항

공유 삭제는 Windows의 네트워크 공유 등록만 제거합니다.

**실제 폴더와 내부 파일은 삭제되지 않으며, NTFS 권한도 자동 제거하지 않습니다.**

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

`main` 브랜치의 버전이 처음 게시되면 GitHub Actions가 빌드와 테스트를 수행하고 같은 버전의 GitHub Release를 자동 생성합니다.

## 구현 방식과 제한사항

- SMB 목록과 작업에는 Windows 11에 기본 포함된 Windows PowerShell 5.1의 `SmbShare` cmdlet을 숨김 프로세스로 사용합니다. 사용자 입력은 스크립트 문자열에 연결하지 않고 프로세스 환경변수로 전달합니다.
- NTFS 권한은 .NET 파일 시스템 ACL API로 처리합니다.
- 공유 이름과 실제 경로 변경은 지원하지 않습니다. 기존 공유를 삭제한 뒤 새로 생성해야 합니다.
- 원격 PC, NAS, 시스템 공유, 네트워크 드라이브, FTP, WebDAV는 관리하지 않습니다.
- 사용자 계정 생성, 방화벽 자동 변경, 네트워크 프로필 자동 변경, 실제 폴더·파일 삭제 기능은 없습니다.
- 실제 SMB, NTFS, UAC 동작은 Windows 11 x64 환경에서 최종 확인해야 합니다.
