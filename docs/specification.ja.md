# DependencyContractAnalyzer 仕様書

この文書は、初回公開版に向けた初期実装スコープを整理したものです。最終完成形の設計全体は `docs/architecture.ja.md` を参照してください。

## 1. 目的

クラスおよびインタフェースに依存契約を宣言し、依存元が要求する契約を依存先が満たしているかを静的解析で検証します。

解析対象は型依存のみであり、DI 登録解析には依存しません。

## 2. 初期スコープ

初回リリースで解析対象とする依存種別は次のとおりです。

| 依存種別 | 対象 |
| --- | --- |
| コンストラクタ引数 | 対象 |
| フィールド型 | 対象 |
| 継承 | 対象 |
| インタフェース実装 | 対象 |

初回リリースでは次を対象外とします。

| 依存種別 | 理由 |
| --- | --- |
| プロパティ | 誤検知防止のため |
| メソッド引数 | 後続のスコープ拡張 |
| `new` | 依存強度が比較的弱いため |
| static 利用 | 依存として扱わないため |

## 3. 属性仕様

### 3.1 提供契約

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = true)]
public sealed class ProvidesContractAttribute : Attribute
{
    public string Name { get; }

    public ProvidesContractAttribute(string name)
    {
        Name = name;
    }
}
```

例:

```csharp
[ProvidesContract("thread-safe")]
public class RedisCacheStore : ICacheStore
{
}
```

### 3.2 依存契約要求

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequiresDependencyContractAttribute : Attribute
{
    public Type DependencyType { get; }
    public string ContractName { get; }

    public RequiresDependencyContractAttribute(Type dependencyType, string contractName)
    {
        DependencyType = dependencyType;
        ContractName = contractName;
    }
}
```

例:

```csharp
[RequiresDependencyContract(typeof(ICacheStore), "thread-safe")]
public class CacheCoordinator
{
    public CacheCoordinator(ICacheStore store)
    {
    }
}
```

## 4. 解析ルール

次の条件をすべて満たす場合に診断を発行します。

1. 型に `RequiresDependencyContractAttribute` が宣言されている
2. 宣言された `DependencyType` に一致する依存が存在する
3. 依存先が必要契約を提供していない

## 5. 契約一致ルール

契約名比較は次のルールで行います。

- 前後空白を削除する
- 大文字小文字を無視する
- Ordinal 比較を使う

`thread-safe`、`THREAD-SAFE`、`Thread-Safe` は同一として扱います。

## 6. 依存関係取得

対象型から次の依存情報を収集します。

### 6.1 コンストラクタ依存

```csharp
public A(B b)
```

取得元:

- `INamedTypeSymbol`
- `Constructors`
- `Parameters`

### 6.2 フィールド依存

```csharp
private B _b;
```

取得元:

- `IFieldSymbol.Type`

### 6.3 継承

```csharp
class A : B
```

取得元:

- `BaseType`

### 6.4 インタフェース実装

```csharp
class A : IFoo
```

取得元:

- `Interfaces`

## 7. 契約取得

依存先から `ProvidesContractAttribute` を取得します。対象は次のとおりです。

- クラス自身
- 実装インタフェース
- 継承元

`Inherited = true` により継承された契約も考慮します。

## 8. 診断仕様

- Diagnostic ID: `DCA001`
- 既定 Severity: `Warning`
- メッセージ: `Dependency '{DependencyType}' does not provide required contract '{ContractName}'.`

Severity は `.editorconfig` により変更可能とします。

## 9. 想定プロジェクト構成

```text
src/
 └ DependencyContractAnalyzer
   ├ Analyzers
   │  └ DependencyContractAnalyzer.cs
   ├ Attributes
   │  ├ ProvidesContractAttribute.cs
   │  └ RequiresDependencyContractAttribute.cs
   ├ Diagnostics
   │  └ DiagnosticDescriptors.cs
   └ Helpers
      └ DependencyGraphBuilder.cs
```

## 10. Analyzer 処理フロー

```text
CompilationStart
        |
        +-- SymbolAction(TypeSymbol)
        |
        +-- RequiresDependencyContractAttribute を取得
        |
        +-- 依存型を収集
        |
        +-- DependencyType に一致する依存を抽出
        |
        +-- ProvidesContractAttribute を確認
        |
        +-- 未提供なら Diagnostic を発行
```

## 11. テスト方針

`Microsoft.CodeAnalysis.Testing` を使用します。

確認すべき代表ケース:

- 依存先が必要契約を提供している場合は Diagnostic なし
- 依存先が必要契約を提供していない場合は `DCA001`

例:

```csharp
[RequiresDependencyContract(typeof(IFoo), "thread-safe")]
class A
{
    public A(IFoo foo) {}
}

class Foo : IFoo {}
```

期待結果: Diagnostic 発生。

## 12. 将来拡張

- `ContractScope` による層・コード領域の宣言
- `RequiresContractOnScope` による scope 単位の契約要求
- `ContractTarget` による型カテゴリ宣言
- `RequiresContractOnTarget` によるカテゴリ単位の契約要求
- `ContractAlias` による契約の包含関係
- メソッド引数依存
- プロパティ依存
- `new` 式解析
- static 利用解析
- 契約階層
- EditorConfig によるポリシー制御

## 13. 非対象

- DI 登録解析
- runtime 依存解決
- Scrutor の挙動
- Factory registration の挙動
- DI コンテナーの挙動

## 14. コーディング規約

- Analyzer の allocation を抑制する
- `ImmutableArray` を優先する
- `SymbolEqualityComparer.Default` を使う
- 文字列比較は ordinal の大文字小文字無視を使う

## 15. 完了条件

- 属性定義を実装する
- Analyzer を実装する
- Diagnostic 発行を実装する
- 単体テストを作成する
- README を整備する
