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
7. nuget.org と `int.nugettest.org` の両方で publish 可能なアカウント名を repository secret `NUGET_USER` に設定します。
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
- publish workflow は単一の repository secret `NUGET_USER` を参照し、nuget.org と `int.nugettest.org` の両方で同じ publish アカウント名を使います。
- manual の `workflow_dispatch` 実行は `develop` と `main` でのみ許可します。
- `develop` からの manual 実行は `https://int.nugettest.org/api/v2/package` へ publish します。
- `main` からの manual 実行は `https://www.nuget.org/api/v2/package` へ publish します。
- publish workflow は release tag が annotated tag であることを検証します。
- tag push の publish 先は trigger 種別ではなく branch により決まり、`main` の tag は `https://www.nuget.org/api/v2/package`、`develop` の tag は `https://int.nugettest.org/api/v2/package` に publish します。
- tag 対象 commit が `main` と `develop` の両方から到達可能、またはどちらからも到達不可能な場合は publish を失敗させます。
- publish 時の pack step では、生成された build output によって package version が勝手に繰り上がらないように、RelaxVersioner の working-directory dirty check を無効化します。
- tag push では upload 前に、生成された `.nupkg` のファイル名が release tag の version と一致することを検証します。
- GitHub Release は `main` 上の release tag から作成します。
- リリースノートは `CHANGELOG.md` と整合させてください。

## ブランチ運用

- `main`: default branch 兼 `nuget.org` 向け安定版 release branch
- `develop`: integration branch 兼 `int.nugettest.org` 向け publish branch

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
- publish 先に対応する branch を確認済み
- annotated tag を intended publish branch から作成済み
