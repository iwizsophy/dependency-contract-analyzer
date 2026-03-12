# Contributing

DependencyContractAnalyzer へのコントリビュートに関心を持っていただきありがとうございます。

## 開始前に

- バグ報告、Analyzer ルール提案、公開 API の変更は、まず Issue で共有してください。
- 変更はできるだけ小さく、焦点を絞ってください。
- Analyzer の挙動を変える場合は、対応するテストを追加または更新してください。
- 最終的な技術判断はメンテナーが行います。

## 開発フロー

1. リポジトリを fork し、作業用ブランチを作成します。
2. 変更内容に応じて Analyzer テストまたはドキュメント更新を行います。
3. ソースプロジェクトが揃っている状態で、リポジトリルートから次を実行してください。
   - `dotnet restore`
   - `dotnet build -c Release --no-restore`
   - `dotnet test -c Release --no-build`
4. Pull Request には次を含めてください。
   - 何を変更したか
   - なぜ変更したか
   - どのように検証したか

## このプロジェクトで重視すること

- `SymbolEqualityComparer.Default` を使った明示的なシンボル比較
- Analyzer 実装における不要な allocation の抑制
- 適切な箇所での `ImmutableArray` 利用
- 契約名比較時の trim と `StringComparison.OrdinalIgnoreCase`

## 関連ドキュメント

- 仕様書: `docs/specification.ja.md`
- 開発ガイド: `docs/development.ja.md`
- Trusted Publishing ガイド: `docs/trusted-publishing.ja.md`
- 行動規範: `CODE_OF_CONDUCT.md`
- サポートポリシー: `.github/SUPPORT.md`
