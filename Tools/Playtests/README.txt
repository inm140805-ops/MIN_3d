LAUNDRY QA — 단일 Unity 자율 입력 검증

프로젝트: D:\3DProject_New / Unity 6000.0.40f1 / SampleScene
일반 게임 실행에는 QA가 자동으로 켜지지 않습니다. Editor 메뉴 Tools > Laundry QA에서만 시작합니다.
현재 열려 있는 Unity Editor 한 개를 재사용합니다. Editor/다른 프로그램을 종료하거나 빌드하지 않습니다.

메뉴
  Run controls regression: 일시정지, 설정 버튼 클릭, 가이드, 개점, 오배송/반납/정답 전달/중복 보상 검사.
  Run a full day: 실제 WASD/마우스/E 입력으로 목표 매출까지 주문을 처리하고 마감.
  Stop owned test: 이 관리자가 시작한 테스트만 중지.
  Show test records: QAOutput/Playtests.
  Intro 검증: Intro 씬을 연 뒤 LaundryPlaytestSession.Begin("intro-controls", 60).
  해당 시나리오는 가상 마우스로 설정/복귀/시작을 클릭하고 SampleScene 로딩을 확인합니다.

입력과 관찰
  GameInput은 일반 실행에서 기존 Input Manager를 사용합니다.
  QA에서만 Input System 가상 Keyboard/Mouse를 만들고 QueueStateEvent로 입력합니다.
  버튼 클릭은 InputSystemUIInputModule의 실제 포인터/레이캐스트 경로를 거칩니다.
  봇은 위치/주문 데이터를 읽어 경로와 옷을 선택합니다. 화면만 보고 판단하는 인간 플레이어 모델은 아닙니다.
  위치 이동, 재화 지급, 타이머 단축, 주문 완료 상태를 직접 설정하지 않습니다.
  스크린샷은 자연스러운 WaitForEndOfFrame 후 캡처합니다. 중첩 PlayerLoop를 호출하지 않습니다.

보호 제한 (앞선 지시문의 더 엄격한 기준도 유지)
  동시에 1세션, 세션 최대300초, 종료 후15초, 한 개선 사이클3시도, 전체12시도.
  한 시나리오3회 연속 실패는 추가 실행 전에 원인을 검토해야 합니다.
  사이클 검토: LaundryPlaytestSession.BeginNextCycle("구체적인 관찰과 수정 내용")
  12시도 제한은 사이클을 바꿔도 초기화되지 않습니다.
  CPU90%, RAM85%, GPU95%가10초 지속되면 소유한 Play Mode를 중단합니다.
  디스크5GB 미만, 자원 측정 누락,10초 프레임 정지, 런타임 오류도 중단합니다.
  시작 시 임계값 이상이면 Play Mode에 들어가지 않습니다.
  숨겨진 Monitor.ps1은 자원을 관찰하고 중지 요청만 남깁니다. 프로세스 강제 종료는 하지 않습니다.
  프로세스 단위 ExecutionPolicy Bypass는 스크립트 실행에만 쓰며 시스템 실행 정책을 바꾸지 않습니다.
  원자적 active.lock은 PID, 프로세스 시작 시각, 프로젝트, 세션ID를 기록합니다.
  활성 소유자의 잠금을 강제로 탈취하지 않습니다.

저장 제한
  세션당10장/1280x720 이하, 로그5MB, QA 임시파일300MB, 최근3개 세션 디렉터리.
  .qa-owned 마커와 경로/재분석 지점을 검증한 QA 하위 폴더만 정리합니다.
  Assets, Library, 기존 사용자 스크린샷은 자동 삭제하지 않습니다.
  비교용 소수 이미지는 QAOutput/baseline에 보존합니다. 영상은 생성하지 않습니다.

검사
  Unity EditMode assembly: Laundry.EditMode.Tests (매출/성공/실패/재도전/인내/대기열/조작 차단)
  PowerShell: powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Playtests/Test-ResourcePolicy.ps1
  Unity를 실행하지 않는 씬/주문 데이터 검사 (44개):
    $qaPython = uv python find --offline --no-python-downloads
    & $qaPython Tools/Playtests/audit_configuration.py
  결과는 QAOutput/ConfigurationAudit.json에 저장합니다. 기존 로컬 Python만 사용합니다.
  게임 검증 결과와 자원 중단은 구분해서 기록합니다. 일부 assertion이 통과해도 중단된 전체 세션은 passed=false입니다.

복구 지점
  D:\3DProject_New_QA\restore-20261116-start
  작업 전부터 수정되어 있던 사용자 파일을 포함한 스냅샷입니다. git reset으로 덮어쓰지 마세요.
  Scripts, SampleScene, Settings, 패키지 설정, ProjectSettings, UI 이미지 import 메타가 보관되어 있습니다.

현재 작업 중간 상태 (2026-11-16 KST, 최종 보고 아님)
  00:48 시작. 약55분 기록은 같은 폴더 RebirthReport.txt에 있습니다.
  입력 기반 단일 주문 완료, 개선 후 오배송/반납/보상/중복 방지, 새 메뉴 설정 클릭/복귀 확인.
  10개 EditMode 회귀 테스트와19개 자원 정책 assertion 통과.
  Intro→설정→복귀→게임 시작 마우스 흐름도 통과했습니다. 최신 HUD 실제 화면은 QAOutput/baseline/after-hud.png입니다.
  RAM85% 지속으로 개선 후 테스트 세 번 중단; full-day는 한 번 시작 보류, 한 번 안전 중단.
  전체 하루 성공과 최신 파티클/작아진 보상 팝업의 최종 화면은 재검증이 필요합니다.
  읽기/쓰기 해제 전후 Editor 텍스처 메모리는150.096MB로 동일했습니다. 메모리 절감 측정 성공으로 간주하지 않습니다.
