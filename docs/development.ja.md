# 開発ガイド

この文書はメンテナーおよびコントリビューター向けです。

## 前提条件

- .NET SDK がインストールされていること
- Roslyn Analyzer 開発の基本知識があること
- `Microsoft.CodeAnalysis.Testing` ベースの単体テストを実行できること

## 典型的なローカル検証

ソースプロジェクトが揃っている状態で、リポジトリルートから次を実行します。

```powershell
dotnet restore DependencyContractAnalyzer.slnx
dotnet build DependencyContractAnalyzer.slnx -c Release --no-restore
dotnet test DependencyContractAnalyzer.slnx -c Release --no-build
```

ローカルでの pack:

```powershell
dotnet pack src/DependencyContractAnalyzer/DependencyContractAnalyzer.csproj -c Release --no-build -o artifacts
```

## 想定プロジェクト構成

初期実装では次の構成を想定しています。

- `src/DependencyContractAnalyzer`: Analyzer、本体属性、Diagnostic、補助ロジック
- `tests/DependencyContractAnalyzer.Tests`: `Microsoft.CodeAnalysis.Testing` ベースの単体テスト
- `docs/`: コントリビューター向け、公開運用向け、仕様書

## 実装上の注意

- Analyzer の allocation はできるだけ抑制する
- Analyzer パスの性能や API の明確さに有効な箇所では `ImmutableArray` を使う
- シンボル比較には `SymbolEqualityComparer.Default` を使う
- 契約名は trim 後に ordinal の大文字小文字無視で比較する
- 初回リリースではコンストラクタ、フィールド、継承、インタフェース実装に対象を限定する

## リリース

NuGet.org への公開方針は `docs/trusted-publishing.ja.md` を参照してください。
