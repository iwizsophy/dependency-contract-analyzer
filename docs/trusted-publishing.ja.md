# Trusted Publishing ガイド

この文書は `DependencyContractAnalyzer` を NuGet フィードへ公開する際の推奨リリースモデルをまとめたものです。

## 推奨方針

長期有効な API キーではなく、GitHub Actions と OpenID Connect を使った NuGet Trusted Publishing を推奨します。

## 設定チェックリスト

1. nuget.org 上で `DependencyContractAnalyzer` パッケージを作成または予約します。
2. `develop` から publish する場合は `int.nugettest.org` 上でも `DependencyContractAnalyzer` パッケージを作成または予約します。
3. この GitHub リポジトリを nuget.org 側パッケージの Trusted Publisher として登録します。
4. `develop` publish を有効にする場合は、この GitHub リポジトリを `int.nugettest.org` 側パッケージの Trusted Publisher としても登録します。
5. nuget.org の Trusted Publishing policy には workflow file 名として `publish.yml` を登録します。`.github/workflows/` のパスは付けません。
6. `develop` publish を有効にする場合は、`int.nugettest.org` 側の Trusted Publishing policy にも同じ `publish.yml` を登録します。
7. publish 可能な nuget.org アカウント名を repository variable `NUGET_PUBLISH_USER` に設定します。
8. publish 可能な `int.nugettest.org` アカウント名を repository variable `NUGETTEST_PUBLISH_USER` に設定します。
9. publish ワークフローで `permissions.id-token: write` を維持します。
10. annotated tag として `v<major>.<minor>.<patch>` 形式の release tag を
   作成して push します。例: `v0.1.0`
11. パッケージ/アセンブリ バージョンは `RelaxVersioner` により git タグから解決します。

## ワークフロー要件

- リポジトリには `push` / `pull_request` 用の `.github/workflows/ci.yml` を含めます。
- リポジトリにはタグ公開用および branch ベースの手動 publish / 検証用 `.github/workflows/publish.yml` を含めます。
- publish 時の認証は `NuGet/login@v1` を利用します。
- OIDC を使える場合、長期 API キーをリポジトリ secret に置かないでください。
- push 前に build、test、pack を完了させてください。
- publish workflow は publish 先に応じて repository variable `NUGET_PUBLISH_USER` と `NUGETTEST_PUBLISH_USER` を使い分けます。
- manual の `workflow_dispatch` 実行は `develop` と `main` でのみ許可します。
- `develop` からの manual 実行は `https://int.nugettest.org/api/v2/package` へ publish します。
- `main` からの manual 実行は検証専用で、
  build/test/pack/artifact upload のみを行い、パッケージ publish は行いません。
- publish 対象の release tag は `main` にマージ済みの commit を指している必要があります。
- publish workflow は release tag が annotated tag であることを検証します。
- annotated release tag の push は `https://www.nuget.org/api/v2/package` へ publish します。
- GitHub Release は `main` 上の release tag から作成します。
- リリースノートは `CHANGELOG.md` と整合させてください。

## ブランチ運用

- `main`: default branch 兼 `nuget.org` 向け安定版 release branch
- `develop`: integration branch 兼 `int.nugettest.org` 向け manual publish branch

## Release チェックリスト

- `CHANGELOG.md` を更新済み
- translation follow-up を確認済み
- docs-sync を確認済み
- dependency 変更を含む場合は `THIRD-PARTY-NOTICES.md` を更新済み
- transitive dependency 監査を完了し、月次 dependency audit issue に
  記録済み
- breaking-change issue を確認済み
- release Pull Request を `main` にマージ済み
- stable version を確認済み
- annotated tag を `main` から作成済み
