# Trusted Publishing ガイド

この文書は `DependencyContractAnalyzer` を NuGet フィードへ公開する際の推奨リリースモデルをまとめたものです。

## 推奨方針

長期有効な API キーではなく、GitHub Actions と OpenID Connect を使った NuGet Trusted Publishing を推奨します。

## 設定チェックリスト

1. nuget.org 上で `DependencyContractAnalyzer` パッケージを作成または予約します。
2. この GitHub リポジトリを対象パッケージの Trusted Publisher として登録します。
3. prerelease を nugettest.org でも検証する場合は、同じワークフローを登録します。
4. publish ワークフローで `permissions.id-token: write` を維持します。
5. `v0.1.0` のようなバージョンタグを作成して push します。
6. パッケージ公開は専用ワークフローで実施します。ファイル名は `.github/workflows/publish.yml` を推奨します。
7. パッケージ/アセンブリ バージョンは `RelaxVersioner` により git タグから解決します。

## ワークフロー要件

- 認証は NuGet Trusted Publishing または `NuGet/login@v1` を利用します。
- OIDC を使える場合、長期 API キーをリポジトリ secret に置かないでください。
- push 前に build、test、pack を完了させてください。
- リリースノートは `CHANGELOG.md` と整合させてください。

## ブランチ運用の一例

- `main`: nuget.org 向け安定版
- `develop` など: nugettest.org 向け prerelease 検証

ブランチ戦略が異なる場合は、リポジトリの運用に合わせて調整してください。
