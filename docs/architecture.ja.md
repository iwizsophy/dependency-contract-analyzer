# DependencyContractAnalyzer アーキテクチャ

この文書は、`DependencyContractAnalyzer` の最終完成形として想定しているアーキテクチャを整理したものです。
リポジトリルートの `ARCHITECTURE.md` を原則と制約の正本とし、
この文書はその詳細版です。

詳細構成、図、依存関係、目標アーキテクチャの説明はこの文書に記載します。
この文書と `ARCHITECTURE.md` が矛盾する場合は、`ARCHITECTURE.md` の原則を優先します。

アーキテクチャ原則の変更が詳細ガイダンスに影響する場合は、
`ARCHITECTURE.md` とこの文書を同じ変更で更新してください。

このツールの芯は次です。

`Type dependency is allowed only when declared contracts are satisfied.`

日本語では、

`型依存は、宣言された契約を満たす場合にのみ許可される。`

これは単なる属性アナライザではなく、次を組み合わせた静的アーキテクチャ検証基盤です。

- 型依存
- 宣言された契約
- target / scope のメタデータ

## 1. 全体像

```text
[Type / Symbol]
    |
    +-- ProvidesContract
    +-- ContractTarget
    +-- ContractScope

[Dependency Extraction]
    |
    +-- どの型がどの型に依存しているかを抽出

[Rule Declaration]
    |
    +-- RequiresDependencyContract
    +-- RequiresContractOnTarget
    +-- RequiresContractOnScope

[Rule Engine]
    |
    +-- 契約一致判定
    +-- 包含グラフ解決
    +-- Roslyn 標準 suppression、exact requirement suppression、owner type exclusion
    +-- Diagnostic 発行
```

## 2. 概念モデル

### Contract

任意文字列の契約です。

例:

- `thread-safe`
- `retry-safe`
- `no-blocking`
- `tenant-aware`

### Provider

契約を提供する型です。

```csharp
[ProvidesContract("thread-safe")]
public class RedisCacheStore : ICacheStore
{
}
```

### Consumer

依存先に契約を要求する型です。

```csharp
[RequiresDependencyContract(typeof(ICacheStore), "thread-safe")]
public class CacheCoordinator
{
    public CacheCoordinator(ICacheStore store) {}
}
```

### Target

`repository` や `controller` のような型カテゴリです。

```csharp
[ContractTarget("repository")]
public class UserRepository : IUserRepository
{
}
```

### Scope

`application` や `repository` のようなアーキテクチャ層またはコード領域です。

```csharp
[ContractScope("application")]
public class OrderService
{
}
```

## 3. 最終形の属性群

### 3.1 提供契約

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = true)]
public sealed class ProvidesContractAttribute : Attribute
{
    public string Name { get; }
    public ProvidesContractAttribute(string name) => Name = name;
}
```

### 3.2 型単位の要求

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

### 3.3 カテゴリ単位の要求

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = true)]
public sealed class ContractTargetAttribute : Attribute
{
    public string Name { get; }
    public ContractTargetAttribute(string name) => Name = name;
}
```

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequiresContractOnTargetAttribute : Attribute
{
    public string TargetName { get; }
    public string ContractName { get; }

    public RequiresContractOnTargetAttribute(string targetName, string contractName)
    {
        TargetName = targetName;
        ContractName = contractName;
    }
}
```

### 3.4 スコープ単位の要求

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Assembly, AllowMultiple = true, Inherited = true)]
public sealed class ContractScopeAttribute : Attribute
{
    public string Name { get; }
    public ContractScopeAttribute(string name) => Name = name;
}
```

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequiresContractOnScopeAttribute : Attribute
{
    public string ScopeName { get; }
    public string ContractName { get; }

    public RequiresContractOnScopeAttribute(string scopeName, string contractName)
    {
        ScopeName = scopeName;
        ContractName = contractName;
    }
}
```

### 3.5 契約包含辺

```csharp
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ContractHierarchyAttribute : Attribute
{
    public string Child { get; }
    public string Parent { get; }

    public ContractHierarchyAttribute(string child, string parent)
    {
        Child = child;
        Parent = parent;
    }
}
```

例:

```csharp
[assembly: ContractHierarchy("snapshot-cache", "immutable")]
[assembly: ContractHierarchy("immutable", "thread-safe")]
```

意味:

`snapshot-cache` を提供していれば `immutable` と `thread-safe` の両方を満たすとみなします。

現在のセマンティクス:

- `child -> parent` を契約包含の有向辺として扱う
- 同じ child に対して属性を繰り返すことで多親を表現できる
- hierarchy の辺は 1 つの DAG として解決する
- `DCA202` はその graph 全体の cycle を報告する
- 契約充足判定はその graph の推移閉包を使う

## 4. ルール評価の優先順位

ルールエンジン内での評価順は明示した方がよいです。

1. `RequiresDependencyContract`
2. `RequiresContractOnTarget`
3. `RequiresContractOnScope`
4. 包含グラフ解決

これは依存評価時の優先順位であり、リリース順とは別です。

## 5. 依存評価モデル

初期実装では、強い依存だけを対象にすれば十分です。

```text
Consumer Type
   +-- constructor parameter
   +-- method parameter
   +-- property
   +-- field
   +-- new expression
   +-- static member usage（`using static` を含む）
   +-- base type
   +-- interface
```

各依存先型について次を参照します。

```text
Dependency Type
   +-- ProvidesContract
   +-- ContractTarget
   +-- ContractScope
```

## 6. 具体例

### 6.1 型単位要求

```csharp
[RequiresDependencyContract(typeof(ICacheStore), "thread-safe")]
public class CacheCoordinator
{
    public CacheCoordinator(ICacheStore store) {}
}
```

```csharp
[ProvidesContract("thread-safe")]
public class RedisCacheStore : ICacheStore
{
}
```

結果: 問題なし。

### 6.2 Target 要求

```csharp
[RequiresContractOnTarget("repository", "thread-safe")]
public class OrderService
{
    public OrderService(IUserRepository repository) {}
}
```

```csharp
[ContractTarget("repository")]
public class UserRepository : IUserRepository
{
}
```

`UserRepository` に `thread-safe` がなければ違反です。

### 6.3 Scope 要求

```csharp
[ContractScope("application")]
[RequiresContractOnScope("repository", "retry-safe")]
public class BillingService
{
    public BillingService(IBillingRepository repository) {}
}
```

```csharp
[ContractScope("repository")]
public class BillingRepository : IBillingRepository
{
}
```

`BillingRepository` が `retry-safe` を持たなければ違反です。

### 6.4 包含グラフベース requirement

```csharp
[assembly: ContractHierarchy("snapshot-cache", "immutable")]
[assembly: ContractHierarchy("immutable", "thread-safe")]
```

```csharp
[ProvidesContract("snapshot-cache")]
public class SnapshotStore : IStore
{
}
```

```csharp
[RequiresDependencyContract(typeof(IStore), "thread-safe")]
public class StoreConsumer
{
    public StoreConsumer(IStore store) {}
}
```

結果: 包含グラフにより適合です。

## 7. 実装アーキテクチャ

Analyzer 本体より先に内部表現を明確にすると実装しやすくなります。

```csharp
internal sealed record ContractDescriptor(string Name);

internal sealed record TargetDescriptor(string Name);

internal sealed record ScopeDescriptor(string Name);

internal sealed record DependencyEdge(
    INamedTypeSymbol Consumer,
    INamedTypeSymbol Dependency,
    DependencyKind Kind);

internal sealed record RequirementDescriptor(
    RequirementKind Kind,
    string SubjectName,
    string ContractName,
    INamedTypeSymbol OwnerType);
```

### 7.1 RequirementKind

```csharp
internal enum RequirementKind
{
    DependencyType,
    Target,
    Scope
}
```

### 7.2 DependencyKind

```csharp
internal enum DependencyKind
{
    ConstructorParameter,
    MethodParameter,
    Property,
    Field,
    ObjectCreation,
    StaticMemberAccess,
    BaseType,
    InterfaceImplementation
}
```

## 8. Analyzer の流れ

```text
CompilationStart
   |
   +-- Attribute symbol を解決
   |
   +-- 型ごとの metadata を抽出
   |     +-- provided contracts
   |     +-- targets
   |     +-- scopes
   |     +-- requirements
   |
   +-- dependency edge を抽出
   |
   +-- rule engine で評価
   |
   +-- diagnostics を発行
```

## 9. Rule Engine の形

本質はこの関数で捉えると分かりやすいです。

```text
Evaluate(consumer, dependency) -> violations
```

内部では次を行います。

1. consumer の requirement を列挙する
2. dependency が requirement の対象に一致するか判定する
3. required contract を満たすか確認する
4. 必要なら包含グラフ解決を適用する
5. それでも満たさなければ diagnostic を発行する

## 10. 診断体系の最終形

### 基本診断

- `DCA001`: 依存先が要求契約を提供していない
- `DCA002`: 指定依存型が存在しない

### 契約定義診断

- `DCA100`: 契約名が空
- `DCA101`: 契約名フォーマット違反
- `DCA102`: 重複契約指定

`DCA101` は contract 名、requirement suppression の contract 引数、hierarchy endpoint のみを対象とし、target 名 / scope 名には適用しません。v1 で適用する形式は lower-kebab-case です。

### ルール定義診断

- `DCA200`: 存在しない target を要求
- `DCA201`: 存在しない scope を要求
- `DCA202`: 契約包含定義が循環している
- `DCA203`: scope 名が空
- `DCA204`: target 名が空
- `DCA205`: target 要求に一致する依存が存在しない
- `DCA206`: scope 要求に一致する依存が存在しない

## 11. 製品方針

`DependencyContractAnalyzer` は、汎用の architecture rule engine を目指すものではありません。

このプロジェクトは、型どうしの関係から導かれる declarative contract と
implementation consistency の検証に集中します。Analyzer は、実装関係が
発生したときに必要な contract、attribute、宣言が正しく揃っているかを検証します。

代表的な関係:

- dependency usage
- interface implementation
- inheritance

スコープ内:

- dependency contract validation
- 型関係から生じる implementation consistency validation
- 特定の実装関係が存在するときに明示宣言を要求する relationship-triggered requirements

明示的な非対象:

- layer dependency enforcement
- namespace / package boundary rules
- generic forbidden dependency graph rules
- architectural layer の cycle detection
- contract と無関係な naming convention analyzer
- file / directory layout rules
- project / solution structure validation
- ArchUnit のような general architecture DSL

設計原則は declarative かつ attribute-based のまま維持します。ルールは
外部 DSL や巨大な設定系ではなく、コード上の明示宣言で表現されるべきです。
