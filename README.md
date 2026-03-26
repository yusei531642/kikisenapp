# KikisenApp

Discord に仮想マイク経由で VOICEVOX の音声を流すための Windows 向け C# アプリです。
文字を入力して `送信` するだけで、VOICEVOX の音声を `VB-CABLE` 経由で Discord に流せます。

## 特徴

- メイン画面は `入力` と `送信` だけのシンプル構成
- 話者、再生先、音量などの細かい設定は別ウィンドウに分離
- `VOICEVOX` は自分でインストールして使う前提
- 設定画面で `VOICEVOX API URL` を変更して接続確認できる
- `VB-CABLE Virtual Audio Device` をインストーラーに同封
- `Whisper` は設定画面の専用タブから、モデルの重さを見て選んでダウンロード
- `setup.exe` 形式のインストーラーを作成可能
- `kikisenapp.png` をアプリアイコンとインストーラーアイコンに使用

## できること

- 文字を入力して VOICEVOX で読み上げ
- 再生先デバイスを選んで、VB-CABLE などの仮想オーディオへ出力
- `VB-CABLE Virtual Audio Device` はインストーラー同封のファイルからセットアップ起動
- `VOICEVOX API URL` を設定画面から保存して接続確認
- Whisper でリアルタイムに聞き取り、文章の終わりごとに VOICEVOX で自動読み上げ
- Discord で使うための手順をアプリ内で確認
- メイン画面は「入力」と「送信」だけにして、細かい設定は `設定` ウィンドウに分離

## 動作環境

- Windows 11 / Windows 10 x64
- VOICEVOX が HTTP API を使える状態で起動していること
- `VB-CABLE` の導入には管理者権限が必要
- Whisper モデルのダウンロード時もインターネット接続が必要

## ダウンロード

- インストーラー形式: Release の `KikisenApp-Setup.exe`
- 単体実行ファイル: Release の `KikisenApp.Desktop.exe`

## 先に知っておいてほしいこと

- VOICEVOX は自分でインストールしてください。
- このアプリは VOICEVOX の API に接続して読み上げます。
- API URL の初期値は `http://127.0.0.1:50021` です。
- `setup.exe` には `VB-CABLE` を同封しています。導入時は管理者権限の許可と、場合によっては再起動が必要です。

## 使い方

1. `KikisenApp-Setup.exe` を起動します。
2. インストーラーで `VB-CABLE` のチェックを入れたまま進めます。
3. `VB-CABLE` のセットアップ画面が開いたら、管理者権限を許可して完了します。
4. 必要なら Windows を再起動します。
5. VOICEVOX を自分でインストールして起動します。
6. アプリを起動して右下の `設定` を押します。
7. `音声設定` タブで `VOICEVOX API URL` を確認して `接続確認` を押します。
8. Discord の入力デバイスを `CABLE Output` にします。
9. `音声設定` タブで再生先を `CABLE Input` にして保存します。
10. `Whisper` タブで入力デバイスを選び、使いたいモデルを選びます。重いモデルほど精度は上がりやすいですが、ダウンロードも起動も重くなります。
11. `モデルをダウンロード` を押して、準備が終わったら `Whisper を開始` を押します。
12. 話した内容は文の終わりごとに自動で VOICEVOX から読み上げられます。
13. 手入力で読み上げたいときは、メイン画面に戻って文字を入れ、`送信` を押します。

## VOICEVOX の準備

- 公式サイト: [VOICEVOX](https://voicevox.hiroshiba.jp/)
- API URL の初期値: `http://127.0.0.1:50021`
- つながらないときは、VOICEVOX が起動しているかと、設定画面の API URL が合っているかを確認してください。

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

GitHub Release を自動化したい場合は、`.github/workflows/release-installer.yml` を使えます。
GitHub Secrets に次の2つを入れると、Release 公開時に `setup.exe` を自動署名してアップロードできます。

- `KIKISENAPP_SIGN_PFX_BASE64`: コードサイニング用 `PFX` を Base64 化した文字列
- `KIKISENAPP_SIGN_PFX_PASSWORD`: その `PFX` のパスワード

Secrets を入れない場合でもワークフローは動きますが、GitHub Actions 上で作られた自己署名証明書になるため、配布先PCでの信頼性は上がりません。

## 主なフォルダ

- `src/KikisenApp.Core`: 設定、VOICEVOX 通信、セットアップ処理
- `src/KikisenApp.Desktop`: Windows フォームの画面
- `tests/KikisenApp.Core.Tests`: 文字整形やアセット選択のテスト

## ライセンス

このプロジェクトは [MIT License](./LICENSE) です。
