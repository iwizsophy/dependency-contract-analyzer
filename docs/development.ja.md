# 開発ガイド

この文書はメンテナーおよびコントリビューター向けです。

## 前提条件

- .NET 10 SDK がインストールされていること
- 完全なテスト実行のために .NET 8、.NET 9、.NET 10 runtime が
  インストールされていること
- Roslyn Analyzer 開発の基本知識があること
- `Microsoft.CodeAnalysis.Testing` ベースの単体テストを実行できること

## 典型的なローカル検証

リポジトリルートから次を実行します。

```powershell
dotnet restore DependencyContractAnalyzer.slnx
dotnet build DependencyContractAnalyzer.slnx -c Release --no-restore -m:1
dotnet test DependencyContractAnalyzer.slnx -c Release --no-build -m:1
```

この test コマンドは `net8.0`、`net9.0`、`net10.0` の単体テスト
スイートを実行します。

ローカルで Cobertura 形式の coverage を取得する場合は次を実行します。

```powershell
dotnet test tests/DependencyContractAnalyzer.Tests/DependencyContractAnalyzer.Tests.csproj -c Release --collect "XPlat Code Coverage" -m:1
```

coverage ファイルは `tests/DependencyContractAnalyzer.Tests/TestResults/**/coverage.cobertura.xml` に出力されます。

ローカルでの pack:

```powershell
dotnet pack src/DependencyContractAnalyzer/DependencyContractAnalyzer.csproj -c Release --no-build -o artifacts
```

## Analyzer test host 方針

`tests/DependencyContractAnalyzer.Tests` は `net8.0`、`net9.0`、
`net10.0` を対象にしています。

verifier は、実行中の test host target framework に一致する
`Microsoft.NETCore.App.Ref` reference assembly を明示的に使います。
platform reference について、`Microsoft.CodeAnalysis.Testing` の
暗黙既定値や runtime からの assembly 発見には依存しません。

これは development と CI で使う current host line に対する内部検証
方針です。公開される `netstandard2.0` analyzer package について、
version ごとの support matrix を示すものではありません。

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
