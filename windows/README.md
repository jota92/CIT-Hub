# CIT Hub for Windows

Windows版はC# / WinUI 3で実装しています。x64とARM64のネイティブ実行形式は別物のため、単一の`exe`ではなく、両方を含む`MSIX Bundle`を配布します。Windowsが端末のCPUに合うパッケージを自動的に選択します。

## Windowsでのビルド

Visual Studio 2022に「.NETデスクトップ開発」とWindows App SDKを導入したうえで、PowerShellから実行します。

```powershell
dotnet restore .\CITHub.Windows\CITHub.Windows.csproj
dotnet publish .\CITHub.Windows\CITHub.Windows.csproj -c Release -r win-x64
dotnet publish .\CITHub.Windows\CITHub.Windows.csproj -c Release -r win-arm64
```

配布時はVisual Studioの「公開」から`MSIX Bundle`を作成し、x64とARM64を選択します。

```powershell
.\build-bundle.ps1
```

## 実装範囲

- 時間割、ToDo、学内サービス、板書、設定のWindows用画面
- WebView2によるmanaba、ポータル、食堂PDF、バスダイヤ、年間予定の表示
- ローカルToDoと板書写真の保存
- Windows資格情報マネージャーによるMARINEアカウント情報・OTP設定の保護

Cloudflareの端末登録、設定同期、通知、ポータル自動ログイン、課題取得はモバイル版と同じ認証契約をWindows用に追加してから有効化します。認証トークンをWindowsアプリに埋め込む実装は行いません。
