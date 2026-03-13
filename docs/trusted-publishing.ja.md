# Trusted Publishing ガイド

この文書は `DependencyContractAnalyzer` を NuGet フィードへ公開する際の推奨リリースモデルをまとめたものです。

## 推奨方針

長期有効な API キーではなく、GitHub Actions と OpenID Connect を使った NuGet Trusted Publishing を推奨します。

## 設定チェックリスト

1. nuget.org 上で `DependencyContractAnalyzer` パッケージを作成または予約します。
2. この GitHub リポジトリを対象パッケージの Trusted Publisher として登録します。
3. nuget.org の Trusted Publishing policy には workflow file 名として `publish.yml` を登録します。`.github/workflows/` のパスは付けません。
4. publish 可能な nuget.org アカウント名を repository variable `NUGET_PUBLISH_USER` に設定します。
5. prerelease を nugettest.org でも検証する場合は、同じワークフローを登録します。
6. publish ワークフローで `permissions.id-token: write` を維持します。
7. `v0.1.0` のようなバージョンタグを作成して push します。
8. パッケージ/アセンブリ バージョンは `RelaxVersioner` により git タグから解決します。

## ワークフロー要件

- リポジトリには `push` / `pull_request` 用の `.github/workflows/ci.yml` を含めます。
- リポジトリにはタグ公開用の `.github/workflows/publish.yml` を含めます。
- publish 時の認証は `NuGet/login@v1` を利用します。
- OIDC を使える場合、長期 API キーをリポジトリ secret に置かないでください。
- push 前に build、test、pack を完了させてください。
- publish workflow は repository variable `NUGET_PUBLISH_USER` を前提にします。
- リリースノートは `CHANGELOG.md` と整合させてください。

## ブランチ運用の一例

- `main`: nuget.org 向け安定版
- `develop` など: nugettest.org 向け prerelease 検証

ブランチ戦略が異なる場合は、リポジトリの運用に合わせて調整してください。
