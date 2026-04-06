# Odin.Core

[![NuGet](https://img.shields.io/nuget/v/Odin.Core)](https://www.nuget.org/packages/Odin.Core) [![License](https://img.shields.io/badge/license-Apache--2.0-blue)](https://github.com/odin-foundation/odin-core-dotnet/blob/main/LICENSE)

Official .NET SDK for [ODIN](https://odin.foundation) (Open Data Interchange Notation) — a canonical data model for transporting meaning between systems, standards, and AI.

## Install

```bash
dotnet add package Odin.Core
```

**Targets:** netstandard2.0, net8.0

## Quick Start

```csharp
using Odin.Core;

var doc = Odin.Parse(@"
{policy}
number = ""PAP-2024-001""
effective = 2024-06-01
premium = #$747.50
active = ?true
");

Console.WriteLine(doc.Get("policy.number")); // "PAP-2024-001"
Console.WriteLine(doc.Get("policy.premium")); // 747.50

var text = Odin.Stringify(doc);
```

## Core API

| Method | Description | Example |
|--------|-------------|---------|
| `Odin.Parse(text)` | Parse ODIN text into a document | `var doc = Odin.Parse(src);` |
| `Odin.Stringify(doc)` | Serialize document to ODIN text | `var text = Odin.Stringify(doc);` |
| `Odin.Canonicalize(doc)` | Deterministic bytes for hashing/signatures | `var bytes = Odin.Canonicalize(doc);` |
| `Odin.Validate(doc, schema)` | Validate against an ODIN schema | `var result = Odin.Validate(doc, schema);` |
| `Odin.ParseSchema(text)` | Parse a schema definition | `var schema = Odin.ParseSchema(src);` |
| `Odin.Diff(a, b)` | Structured diff between two documents | `var changes = Odin.Diff(docA, docB);` |
| `Odin.Patch(doc, diff)` | Apply a diff to a document | `var updated = Odin.Patch(doc, changes);` |
| `Odin.ParseTransform(text)` | Parse a transform specification | `var tx = Odin.ParseTransform(src);` |
| `Odin.ExecuteTransform(tx, source)` | Run a transform on data | `var result = Odin.ExecuteTransform(tx, doc);` |
| `doc.ToJson()` | Export to JSON | `var json = doc.ToJson();` |
| `doc.ToXml()` | Export to XML | `var xml = doc.ToXml();` |
| `doc.ToCsv()` | Export to CSV | `var csv = doc.ToCsv();` |
| `Odin.Stringify(doc)` | Export to ODIN | `var odin = Odin.Stringify(doc);` |
| `Odin.Builder()` | Fluent document builder | `Odin.Builder().Section("policy")...` |
| `Odin.CreateStreamingParser(handler)` | Streaming/SAX-style parser | `Odin.CreateStreamingParser(handler);` |

## Schema Validation

```csharp
using Odin.Core;

var schema = Odin.ParseSchema(@"
{policy}
!number : string
!effective : date
!premium : currency
active : boolean
");

var doc = Odin.Parse(source);
var result = Odin.Validate(doc, schema);

if (!result.Valid)
{
    foreach (var error in result.Errors)
        Console.WriteLine(error);
}
```

## Transforms

```csharp
using Odin.Core;

var transform = Odin.ParseTransform(@"
map policy -> record
  policy.number -> record.id
  policy.premium -> record.amount
");

var result = Odin.ExecuteTransform(transform, doc);
```

## Export

```csharp
var odin = Odin.Stringify(doc); // ODIN string
var json = doc.ToJson();       // JSON string
var xml  = doc.ToXml();        // XML string
var csv  = doc.ToCsv();        // CSV string
```

## Builder

```csharp
var doc = Odin.Builder()
    .Section("policy")
    .Set("number", "PAP-2024-001")
    .Set("effective", new DateOnly(2024, 6, 1))
    .Set("premium", new OdinCurrency(747.50m))
    .Set("active", true)
    .Build();
```

## Streaming Parser

```csharp
Odin.CreateStreamingParser(new OdinStreamHandler
{
    OnSectionStart = name => Console.WriteLine($"Section: {name}"),
    OnField = (key, value) => Console.WriteLine($"{key} = {value}"),
    OnSectionEnd = name => { }
});
```

## Testing

Tests use [xUnit](https://xunit.net/) and the shared golden test suite:

```bash
dotnet test
```

## Links

- [.Odin Foundation Website](https://odin.foundation)
- [GitHub](https://github.com/odin-foundation/odin)
- [Golden Test Suite](https://github.com/odin-foundation/odin/tree/main/sdk/golden)
- [License (Apache 2.0)](https://github.com/odin-foundation/odin/blob/main/LICENSE)
