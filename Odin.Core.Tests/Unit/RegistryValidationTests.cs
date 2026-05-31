using System;
using System.Linq;
using Odin.Core.Resolver;
using Odin.Core.Types;
using Odin.Core.Validation;
using Xunit;

namespace Odin.Core.Tests.Unit;

public class RegistryValidationTests
{
    private static int CountV013(ValidationResult result)
    {
        return result.Errors.Count(e => e.ErrorCode == ValidationErrorCode.UnresolvedReference);
    }

    [Fact]
    public void TopLevelImportedTypeRef_V013_DropsFromOneToZero_WithRegistry()
    {
        var reader = new InMemoryFileReader();
        reader.AddFile("/main.schema.odin", "@import \"types.schema.odin\" as types\n\n{policy}\nstatus = !@types.policy_status\n");
        reader.AddFile("/types.schema.odin", "{@policy_status}\ncode = ! string\n");

        var schema = Core.Odin.ParseSchema(reader.ReadFile("/main.schema.odin"));
        Assert.True(schema.Fields["policy.status"].FieldType is TypeRefFieldType);

        var noRegistry = ValidationEngine.Validate(OdinDocument.Empty(), schema, ValidateOptions.Default);
        Assert.Equal(1, CountV013(noRegistry));

        var resolver = new ImportResolver(reader);
        resolver.SchemaParser = Core.Odin.ParseSchema;
        resolver.DocumentParser = Core.Odin.Parse;
        var resolved = resolver.ResolveSchema("/main.schema.odin");

        var withRegistry = ValidationEngine.Validate(OdinDocument.Empty(), schema, ValidateOptions.Default, resolved.TypeRegistry);
        Assert.Equal(0, CountV013(withRegistry));
    }

    [Fact]
    public void ValidateWithImports_ResolvesImportedTypeRef()
    {
        var reader = new InMemoryFileReader();
        reader.AddFile("/main.schema.odin", "@import \"types.schema.odin\" as types\n\n{policy}\nstatus = !@types.policy_status\n");
        reader.AddFile("/types.schema.odin", "{@policy_status}\ncode = ! string\n");

        var result = Core.Odin.ValidateWithImports(OdinDocument.Empty(), "/main.schema.odin", reader);
        Assert.Equal(0, CountV013(result));
    }

    [Fact]
    public void TypeInternalTypeRef_DoesNotEmitV013_WithoutRegistry()
    {
        // @typeRef inside a type definition only resolves when the type is used;
        // it must not surface as a root-level unresolved reference (V013).
        var schema = Core.Odin.ParseSchema("{@policy}\nstatus = @policy_status\n{@policy_status}\ncode = ! string\n");
        var result = ValidationEngine.Validate(OdinDocument.Empty(), schema, ValidateOptions.Default);
        Assert.Equal(0, CountV013(result));
    }

    [Fact]
    public void RelativeSubsection_NestsIntoType_NotRoot()
    {
        // {.term} inside {@policy} nests into the policy type, not the schema root.
        var schema = Core.Odin.ParseSchema("{@policy}\nnumber = !\n{.term}\neffective = !date\nexpiration = !date\n");

        var policy = schema.Types["policy"];
        var fieldNames = policy.SchemaFields.Select(f => f.Name).ToHashSet();
        Assert.Contains("term.effective", fieldNames);
        Assert.Contains("term.expiration", fieldNames);
        Assert.DoesNotContain(schema.Fields.Keys, k => k.Contains("term.", StringComparison.Ordinal));

        // Empty document yields no root required-field errors (term.* live on the type).
        var errors = ValidationEngine.Validate(OdinDocument.Empty(), schema, ValidateOptions.Default).Errors;
        Assert.DoesNotContain(errors, e => e.ErrorCode == ValidationErrorCode.RequiredFieldMissing);
    }
}
