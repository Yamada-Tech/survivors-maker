# Survivors Maker

Vampire Survivors風ゲームを **プログラミング不要** で制作できるゲームエディタツール。

## 概要

Survivors Makerは、Vampire Survivorsライクなローグライト・サバイバーアクションゲームを
誰でも作成できるスタンドアロンツールです。

- 🗺️ **MAPエディタ** — タイルベースでマップを自由に構築
- 👾 **敵エディタ** — 敵の種類・パラメータ・Wave構成を設定
- ⚔️ **武器エディタ** — 近接・投射・範囲など武器タイプを定義
- 🎮 **即時テストプレイ** — エディタからワンクリックでプレイモードへ
- 🎨 **アセット差し替え** — スプライト・BGM・SEを自由に入れ替え

## 技術スタック

| 項目 | 内容 |
|---|---|
| エンジン | Unity 6 LTS |
| レンダリング | URP (2D Renderer) |
| UI | UI Toolkit |
| データ形式 | JSON |
| プラットフォーム | Windows / macOS / Linux |

## セットアップ

1. Unity Hub から **Unity 6 LTS** をインストール
2. このリポジトリをクローン
3. Unity Hub で `Add project from disk` → クローンしたフォルダを指定
4. URP 2D テンプレートの設定が自動適用されます

## ビルド手順

### ローカルビルド（Unity Editor）

1. Unityでプロジェクトを開く
2. メニューから以下を選択してビルドする
   - `Build/Build Windows`
   - `Build/Build macOS`
   - `Build/Build Linux`
3. ビルド成果物は `Builds/` 配下に出力されます

`Assets/Scripts/Build/BuildScript.cs` で、ビルド前に `Assets/StreamingAssets/ProjectData` のデフォルトデータ一式（`player.json` / `map.json` / `enemies.json` / `weapons.json` / `waves.json`）の存在確認を行います。不足がある場合は `BuildFailedException` でビルドを中断します。

### CI/CDビルド（GitHub Actions）

- `.github/workflows/build.yml` により `main` ブランチへの `push` / `pull_request` をトリガーに自動ビルドを実行
- リポジトリの `Settings > Secrets and variables > Actions` に以下を登録
  - `UNITY_LICENSE`
  - `UNITY_EMAIL`
  - `UNITY_PASSWORD`
- 対象プラットフォーム
  - `StandaloneWindows64`
  - `StandaloneOSX`
  - `StandaloneLinux64`
- ビルド成果物は `Builds/<targetPlatform>` に出力され、Actionsのartifactとしてアップロードされます

## ライセンス

未定
