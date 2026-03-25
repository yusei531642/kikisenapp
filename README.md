# KikisenApp

Discord に仮想マイク経由で VOICEVOX の音声を流すための Windows 向け C# アプリです。

## できること

- 文字を入力して VOICEVOX で読み上げ
- 再生先デバイスを選んで、VB-CABLE などの仮想オーディオへ出力
- VOICEVOX ENGINE の最新 Windows GPU 版を自動ダウンロード
- VB-CABLE Virtual Audio Device を公式サイトから自動ダウンロードしてセットアップ起動
- Discord で使うための手順をアプリ内で確認

## 先に知っておいてほしいこと

- VOICEVOX ENGINE はアプリから自動取得できます。
- このPCでは GPU を見て、`NVIDIA版` を優先し、使えない場合は `DirectML版` に切り替えます。
- `VB-CABLE` は公式サイトから自動取得してセットアップを起動します。
- ただし、Windows の仕様で管理者権限の許可と、場合によっては再起動が必要です。

## 開発環境の準備

このリポジトリではローカルに .NET SDK を入れてビルドできます。

```powershell
New-Item -ItemType Directory -Force .tools | Out-Null
Invoke-WebRequest -Uri https://dot.net/v1/dotnet-install.ps1 -OutFile .tools\dotnet-install.ps1
& powershell -ExecutionPolicy Bypass -File .tools\dotnet-install.ps1 -Version 8.0.412 -InstallDir .tools\dotnet
```

## 実行方法

```powershell
$env:PATH = "D:\program\kikisenapp\.tools\dotnet;" + $env:PATH
dotnet run --project .\src\KikisenApp.Desktop\KikisenApp.Desktop.csproj
```

もっと簡単に起動したい場合:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-app.ps1
```

配布用の EXE を作りたい場合:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish.ps1
```

この `publish` では、このPCの現在ユーザーだけが信頼するローカル証明書を作って EXE に署名します。
そのため、このPC上では `不明な発行元` の警告を減らしやすくなります。

ただし、外部配布用の正式な安全表示までは保証できません。
ほかのPCでも警告を減らしたい場合は、商用のコードサイニング証明書が必要です。

## 初回セットアップの流れ

1. アプリの `セットアップ` タブで `VOICEVOX を自動セットアップ` を押します。
2. `VOICEVOX を起動` を押してエンジンを立ち上げます。
3. `VB-CABLE を自動セットアップ` を押して、管理者権限を許可します。
4. 必要なら Windows を再起動します。
5. Discord の入力デバイスを `CABLE Output` にします。
6. `しゃべる` タブで再生先を `CABLE Input` にして読み上げます。

## 主なフォルダ

- `src/KikisenApp.Core`: 設定、VOICEVOX 通信、セットアップ処理
- `src/KikisenApp.Desktop`: Windows フォームの画面
- `tests/KikisenApp.Core.Tests`: 文字整形やアセット選択のテスト
