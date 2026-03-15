# Contributing

DependencyContractAnalyzer へのコントリビュートに関心を持っていただきありがとうございます。

## 開始前に

- contributor 向けの変更では、次の順でガバナンス文書を参照してください:
  `SPECIFICATION.md`、`ARCHITECTURE.md`、`DECISIONS.md`、`AGENTS.md`
- GitHub は必要に応じて次の順で使ってください: 探索は Discussion、
  決定は Issue、実装は Pull Request。論点が明確なら最初から Issue で構いません。
- 大きな変更、公開 API 変更、利用者に見える挙動変更、設計判断、
  ガバナンス文書変更、バグ報告、Analyzer ルール提案は、まず Issue で共有してください。
- ガバナンス文書の editorial-only 変更は Issue 不要ですが、
  maintainer review は必須です。
- 変更はできるだけ小さく、焦点を絞ってください。
- GitHub の default branch は `develop` です。
- Pull Request の既定ターゲットは `develop` です。
- ブランチフローは `feature/* -> develop`、`bugfix/* -> develop`、
  `chore/* -> develop`、release 時の `develop -> main` とします。
- `main` と `develop` への直接 push は禁止です。
- Analyzer の挙動を変える場合は、対応するテストと関連仕様文書を追加または更新してください。
- 仕様や公開 API に対する高影響変更は、可能な限り同じ変更で
  `SPECIFICATION.md` と `DECISIONS.md` も更新してください。
- pre-release の breaking change は `CHANGELOG.md` の
  `## Breaking Changes` セクションも更新してください。
- `develop` から `main` への release PR は merge commit を使います。
- `develop` に入る feature / bugfix PR は squash merge を推奨します。
- release tag は annotated tag とし、
  `v<major>.<minor>.<patch>` 形式を使い、`main` にマージ済みの
  commit からのみ作成します。
- GitHub Release は `main` 上の release tag から作成します。
- manual publish workflow dispatch は検証専用で、`develop` と `main`
  でのみ実行します。
- 現在の required CI check は `build-test-pack` です。
- 新しい third-party dependency の追加には Issue が必要です。
- major update や dependency の置き換えにも Issue が必要です。
- dependency を追加または更新する場合は、同じ Pull Request で
  `THIRD-PARTY-NOTICES.md` も更新してください。
- `THIRD-PARTY-NOTICES.md` には runtime / build-time / dev-time を含む
  直接依存全体を記録します。
- transitive dependency は既定では
  `THIRD-PARTY-NOTICES.md` に載せず、
  `dotnet list package --include-transitive`、Dependabot、
  GitHub security advisories で監査します。
- transitive dependency の監査は最低でも月 1 回と、各 release 前に実施します。
- 月次の transitive dependency 監査は maintainer が担当し、
  専用の GitHub Issue
  （例: `Monthly Dependency Audit`）に月次コメントで記録します。
- Dependabot は `develop` 向けの weekly な NuGet と GitHub Actions の
  update proposal に使います。
- Dependabot Pull Request も通常の Pull Request と同様に扱い、
  CI pass、maintainer review、手動 merge を必須とします。
  auto-merge は使いません。
- MIT、Apache-2.0、BSD などの permissive license は原則許容とし、
  copyleft や制約付き license は maintainer の明示 review を必要とします。
- 最終的な技術判断はメンテナーが行い、高影響変更の承認は GitHub 上に記録してください。

## 開発フロー

1. リポジトリを fork し、作業用ブランチを作成します。
2. 設計探索が必要な場合は、Discussion を作成します。
3. 挙動やアーキテクチャや運用判断に影響する場合は、Issue を作成または更新します。
4. ADR を提案する場合は、`ADR proposal: <title>` というタイトルの Issue を使います。
5. 変更が影響する場合は、仕様・アーキテクチャ・決定記録も更新します。
6. 緊急の一時修正を入れる場合は、メンテナーまたは reviewer の確認を取り、
   同じ Pull Request 内で follow-up Issue を作成します。
7. 変更内容に応じて Analyzer テストまたはドキュメント更新を行います。
8. ソースプロジェクトが揃っている状態で、リポジトリルートから次を実行してください。
   - `dotnet restore`
   - `dotnet build -c Release --no-restore`
   - `dotnet test -c Release --no-build`
9. Pull Request には次を含めてください。
   - 何を変更したか
   - なぜ変更したか
   - どのように検証したか

`main` または `develop` に直接 push が入った場合は、原則 revert し、
通常の Pull Request フローで入れ直してください。maintainer が緊急対応として
明示承認した場合のみ例外を認め、その理由を Issue または Pull Request comment
に記録します。

## このプロジェクトで重視すること

- `SymbolEqualityComparer.Default` を使った明示的なシンボル比較
- Analyzer 実装における不要な allocation の抑制
- 適切な箇所での `ImmutableArray` 利用
- 契約名比較時の trim と `StringComparison.OrdinalIgnoreCase`

## 関連ドキュメント

- ガバナンス要約仕様: `SPECIFICATION.md`
- ガバナンスアーキテクチャ原則: `ARCHITECTURE.md`
- アーキテクチャ決定記録: `DECISIONS.md`
- リポジトリ運用ルール: `AGENTS.md`
- 詳細仕様書: `docs/specification.ja.md`
- 詳細アーキテクチャ: `docs/architecture.ja.md`
- 開発ガイド: `docs/development.ja.md`
- Trusted Publishing ガイド: `docs/trusted-publishing.ja.md`
- 行動規範: `CODE_OF_CONDUCT.md`
- サポートポリシー: `.github/SUPPORT.md`
