# KikisenApp

Discord に仮想マイク経由で VOICEVOX の音声を流すための Windows 向け C# アプリです。
文字を入力して `送信` するだけで、VOICEVOX の音声を `VB-CABLE` 経由で Discord に流せます。

## 特徴

- メイン画面は `入力` と `送信` だけのシンプル構成
- 話者、再生先、音量などの細かい設定は別ウィンドウに分離
- `VOICEVOX ENGINE` は `setup.exe` 側で導入
- `VB-CABLE Virtual Audio Device` をインストーラーに同封
- `setup.exe` 形式のインストーラーを作成可能
- `kikisenapp.png` をアプリアイコンとインストーラーアイコンに使用

## できること

- 文字を入力して VOICEVOX で読み上げ
- 再生先デバイスを選んで、VB-CABLE などの仮想オーディオへ出力
- `setup.exe` 実行中に VOICEVOX ENGINE の最新 Windows GPU 版を外部ダウンロード
- `VB-CABLE Virtual Audio Device` はインストーラー同封のファイルからセットアップ起動
- `setup.exe` 後にアプリを開くと、見つかった `VOICEVOX ENGINE` を自動で起動しやすい
- アプリ起動時に `VOICEVOX ENGINE` の新しい GitHub Release を自動確認して、必要なら自動ダウンロード
- Discord で使うための手順をアプリ内で確認
- メイン画面は「入力」と「送信」だけにして、細かい設定は `設定` ウィンドウに分離

## 動作環境

- Windows 11 / Windows 10 x64
- NVIDIA GPU があれば `VOICEVOX ENGINE NVIDIA版` を優先
- NVIDIA GPU がない場合は `DirectML版` を使用
- `setup.exe` 実行時は VOICEVOX ダウンロードのためインターネット接続が必要
- `VB-CABLE` の導入には管理者権限が必要

## ダウンロード

- インストーラー形式: Release の `KikisenApp-Setup.exe`
- 単体実行ファイル: Release の `KikisenApp.Desktop.exe`

## 先に知っておいてほしいこと

- VOICEVOX ENGINE は `setup.exe` 側で入れる前提です。
- このPCでは GPU を見て、`NVIDIA版` を優先し、使えない場合は `DirectML版` に切り替えます。
- `setup.exe` では `VB-CABLE` を同封し、`VOICEVOX ENGINE` はセットアップ中に外部ダウンロードします。
- ただし、Windows の仕様で管理者権限の許可と、場合によっては再起動が必要です。

## 使い方

1. `KikisenApp-Setup.exe` を起動します。
2. インストーラーで `VOICEVOX ENGINE` と `VB-CABLE` のチェックを入れたまま進めます。
3. `VOICEVOX ENGINE` の外部ダウンロード完了まで待ちます。
4. `VB-CABLE` のセットアップ画面が開いたら、管理者権限を許可して完了します。
5. 必要なら Windows を再起動します。
6. アプリを起動して右下の `設定` を押します。
7. ふつうは自動で `VOICEVOX` を見つけて起動します。うまくいかないときだけ `VOICEVOX を起動` を押します。
8. Discord の入力デバイスを `CABLE Output` にします。
9. `音声設定` タブで再生先を `CABLE Input` にして保存します。
10. メイン画面に戻って文字を入れ、`送信` を押します。

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

`setup.exe` 形式のインストーラーを作りたい場合:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1
```

作成先:

- `dist\KikisenApp-Setup.exe`

この `publish` では、このPCの現在ユーザーだけが信頼するローカル証明書を作って EXE に署名します。
そのため、このPC上では `不明な発行元` の警告を減らしやすくなります。

ただし、外部配布用の正式な安全表示までは保証できません。
ほかのPCでも警告を減らしたい場合は、商用のコードサイニング証明書が必要です。

## 主なフォルダ

- `src/KikisenApp.Core`: 設定、VOICEVOX 通信、セットアップ処理
- `src/KikisenApp.Desktop`: Windows フォームの画面
- `tests/KikisenApp.Core.Tests`: 文字整形やアセット選択のテスト

## ライセンス

このプロジェクトは [MIT License](./LICENSE) です。
