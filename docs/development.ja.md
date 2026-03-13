# 開発ガイド

この文書はメンテナーおよびコントリビューター向けです。

## 前提条件

- .NET SDK がインストールされていること
- Roslyn Analyzer 開発の基本知識があること
- `Microsoft.CodeAnalysis.Testing` ベースの単体テストを実行できること

## 典型的なローカル検証

リポジトリルートから次を実行します。

```powershell
dotnet restore DependencyContractAnalyzer.slnx
dotnet build DependencyContractAnalyzer.slnx -c Release --no-restore
dotnet test DependencyContractAnalyzer.slnx -c Release --no-build
```

ローカルで Cobertura 形式の coverage を取得する場合は次を実行します。

```powershell
dotnet test tests/DependencyContractAnalyzer.Tests/DependencyContractAnalyzer.Tests.csproj -c Release --collect "XPlat Code Coverage"
```

coverage ファイルは `tests/DependencyContractAnalyzer.Tests/TestResults/**/coverage.cobertura.xml` に出力されます。

ローカルでの pack:

```powershell
dotnet pack src/DependencyContractAnalyzer/DependencyContractAnalyzer.csproj -c Release --no-build -o artifacts
```

## プロジェクト構成

現在のリポジトリ構成は次のとおりです。

- `src/DependencyContractAnalyzer`: Analyzer、本体属性、Diagnostic、補助ロジック
- `samples/DependencyContractAnalyzer.Sample`: clean build を前提にした実行可能な consumer 例。代表的な invalid case は sample README に分離
- `tests/DependencyContractAnalyzer.Tests`: `Microsoft.CodeAnalysis.Testing` ベースの単体テスト
- `docs/`: コントリビューター向け、公開運用向け、仕様書

## 実装上の注意

- Analyzer の allocation はできるだけ抑制する
- Analyzer パスの性能や API の明確さに有効な箇所では `ImmutableArray` を使う
- シンボル比較には `SymbolEqualityComparer.Default` を使う
- 契約名は trim 後に ordinal の大文字小文字無視で比較する
- 初回リリースではコンストラクタ、コンストラクタ以外のメソッド引数、プロパティ型、フィールド、`new` 式、static メンバー利用、継承、インタフェース実装に対象を限定する

## リリース

- CI 検証は `.github/workflows/ci.yml` で定義しています。
- CI では package artifact に加えて `dotnet test` の test result / coverage artifact も保存します。
- NuGet.org への公開方針は `docs/trusted-publishing.ja.md` を参照してください。
- リリース publish は `.github/workflows/publish.yml` で定義しています。
