# Trusted Publishing ガイド

この文書は `DependencyContractAnalyzer` を NuGet フィードへ公開する際の推奨リリースモデルをまとめたものです。

## 推奨方針

長期有効な API キーではなく、GitHub Actions と OpenID Connect を使った NuGet Trusted Publishing を推奨します。

## 設定チェックリスト

1. nuget.org 上で `DependencyContractAnalyzer` パッケージを作成または予約します。
2. この GitHub リポジトリを対象パッケージの Trusted Publisher として登録します。
3. nuget.org の Trusted Publishing policy には workflow file 名として `publish.yml` を登録します。`.github/workflows/` のパスは付けません。
4. publish 可能な nuget.org アカウント名を repository variable `NUGET_PUBLISH_USER` に設定します。
5. nugettest.org で publish workflow の動作確認を行う場合は、同じワークフローを登録します。これは任意の検証であり、通常の release flow には含みません。
6. publish ワークフローで `permissions.id-token: write` を維持します。
7. annotated tag として `v<major>.<minor>.<patch>` 形式の release tag を
   作成して push します。例: `v0.1.0`
8. パッケージ/アセンブリ バージョンは `RelaxVersioner` により git タグから解決します。

## ワークフロー要件

- リポジトリには `push` / `pull_request` 用の `.github/workflows/ci.yml` を含めます。
- リポジトリにはタグ公開用および手動検証用の `.github/workflows/publish.yml` を含めます。
- publish 時の認証は `NuGet/login@v1` を利用します。
- OIDC を使える場合、長期 API キーをリポジトリ secret に置かないでください。
- push 前に build、test、pack を完了させてください。
- publish workflow は repository variable `NUGET_PUBLISH_USER` を前提にします。
- manual の `workflow_dispatch` 実行は検証専用で、
  build/test/pack/artifact upload のみを行い、パッケージ publish は行いません。
- manual の検証実行は `develop` と `main` でのみ許可します。
- publish 対象の release tag は `main` にマージ済みの commit を指している必要があります。
- annotated tag は今すぐ必須です。workflow 側の厳密検証は別途追跡する自動化タスクとして後追いで追加できます。
- `nugettest.org` は publish 経路の検証用途には使えますが、通常の release flow には含めません。正式 release は `main` 上の stable tag から `nuget.org` にのみ publish します。
- GitHub Release は `main` 上の release tag から作成します。
- リリースノートは `CHANGELOG.md` と整合させてください。

## ブランチ運用

- `main`: default branch 兼 nuget.org 向け安定版 release branch
- `develop`: integration branch

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
