using System.Collections.Generic;
using Odin.Core;
using Odin.Core.Types;
using Odin.Core.Validation;
using Xunit;

namespace Odin.Core.Tests.Unit;

public class ValidatorExtendedTests
{
    // ─────────────────────────────────────────────────────────────────
    // Schema Parsing Basics
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseSchema_EmptySchema()
    {
        var schema = Core.Odin.ParseSchema("");
        Assert.NotNull(schema);
        Assert.Empty(schema.Fields);
    }

    [Fact]
    public void ParseSchema_WithMetadata()
    {
        var schema = Core.Odin.ParseSchema("{$}\nid = \"test-schema\"\ntitle = \"Test Schema\"");
        Assert.Equal("test-schema", schema.Metadata.Id);
        Assert.Equal("Test Schema", schema.Metadata.Title);
    }

    [Fact]
    public void ParseSchema_StringField()
    {
        var schema = Core.Odin.ParseSchema("name = string");
        Assert.True(schema.Fields.ContainsKey("name"));
        Assert.IsType<StringFieldType>(schema.Fields["name"].FieldType);
    }

    [Fact]
    public void ParseSchema_IntegerField()
    {
        var schema = Core.Odin.ParseSchema("age = ## :required");
        Assert.True(schema.Fields.ContainsKey("age"));
        Assert.IsType<IntegerFieldType>(schema.Fields["age"].FieldType);
        Assert.True(schema.Fields["age"].Required);
    }

    [Fact]
    public void ParseSchema_BooleanField()
    {
        var schema = Core.Odin.ParseSchema("active = ?");
        Assert.True(schema.Fields.ContainsKey("active"));
        Assert.IsType<BooleanFieldType>(schema.Fields["active"].FieldType);
    }

    [Fact]
    public void ParseSchema_NumberField()
    {
        var schema = Core.Odin.ParseSchema("rate = #");
        Assert.True(schema.Fields.ContainsKey("rate"));
        Assert.IsType<NumberFieldType>(schema.Fields["rate"].FieldType);
    }

    [Fact]
    public void ParseSchema_CurrencyField()
    {
        var schema = Core.Odin.ParseSchema("price = #$");
        Assert.True(schema.Fields.ContainsKey("price"));
        Assert.IsType<CurrencyFieldType>(schema.Fields["price"].FieldType);
    }

    [Fact]
    public void ParseSchema_RequiredModifier_Prefix()
    {
        var schema = Core.Odin.ParseSchema("name = ! string");
        Assert.True(schema.Fields["name"].Required);
    }

    [Fact]
    public void ParseSchema_ConfidentialModifier()
    {
        var schema = Core.Odin.ParseSchema("ssn = * string");
        Assert.True(schema.Fields["ssn"].Confidential);
    }

    [Fact]
    public void ParseSchema_DeprecatedModifier()
    {
        var schema = Core.Odin.ParseSchema("old = :deprecated string");
        Assert.True(schema.Fields["old"].Deprecated);
    }

    // ─────────────────────────────────────────────────────────────────
    // V001: Required Field Missing
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_RequiredFieldPresent_Valid()
    {
        var schema = Core.Odin.ParseSchema("name = ! string");
        var doc = Core.Odin.Parse("name = \"Alice\"");
        var result = Core.Odin.Validate(doc, schema);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_RequiredFieldMissing_Error()
    {
        var schema = Core.Odin.ParseSchema("name = ! string");
        var doc = Core.Odin.Parse("age = ##30");
        var result = Core.Odin.Validate(doc, schema);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == ValidationErrorCode.RequiredFieldMissing);
    }

    [Fact]
    public void Validate_MultipleRequiredFields_AllPresent()
    {
        var schema = Core.Odin.ParseSchema("name = ! string\nage = ! ##");
        var doc = Core.Odin.Parse("name = \"Alice\"\nage = ##30");
        var result = Core.Odin.Validate(doc, schema);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_MultipleRequiredFields_OneMissing()
    {
        var schema = Core.Odin.ParseSchema("name = ! string\nage = ! ##");
        var doc = Core.Odin.Parse("name = \"Alice\"");
        var result = Core.Odin.Validate(doc, schema);
        Assert.False(result.IsValid);
    }

    // ─────────────────────────────────────────────────────────────────
    // V002: Type Mismatch
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_TypeMatch_StringField()
    {
        var schema = Core.Odin.ParseSchema("name = string");
        var doc = Core.Odin.Parse("name = \"Alice\"");
        var result = Core.Odin.Validate(doc, schema);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_TypeMismatch_IntegerForString()
    {
        var schema = Core.Odin.ParseSchema("name = string");
        var doc = Core.Odin.Parse("name = ##42");
        var result = Core.Odin.Validate(doc, schema);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == ValidationErrorCode.TypeMismatch);
    }

    [Fact]
    public void Validate_TypeMatch_IntegerField()
    {
        var schema = Core.Odin.ParseSchema("age = ##");
        var doc = Core.Odin.Parse("age = ##30");
        var result = Core.Odin.Validate(doc, schema);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_TypeMismatch_StringForInteger()
    {
        var schema = Core.Odin.ParseSchema("age = ##");
        var doc = Core.Odin.Parse("age = \"thirty\"");
        var result = Core.Odin.Validate(doc, schema);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_TypeMatch_BooleanField()
    {
        var schema = Core.Odin.ParseSchema("active = ?");
        var doc = Core.Odin.Parse("active = true");
        var result = Core.Odin.Validate(doc, schema);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_NullAllowed_ForOptionalField()
    {
        var schema = Core.Odin.ParseSchema("name = string");
        var doc = Core.Odin.Parse("name = ~");
        var result = Core.Odin.Validate(doc, schema);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_NullNotAllowed_ForRequiredField()
    {
        var schema = Core.Odin.ParseSchema("name = ! string");
        var doc = Core.Odin.Parse("name = ~");
        var result = Core.Odin.Validate(doc, schema);
        Assert.False(result.IsValid);
    }

    // ─────────────────────────────────────────────────────────────────
    // V003: Value Out of Bounds
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_NumberInBounds()
    {
        var schema = Core.Odin.ParseSchema("age = ## :(0..150)");
        var doc = Core.Odin.Parse("age = ##30");
        var result = Core.Odin.Validate(doc, schema);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_NumberBelowMinimum()
    {
        var schema = Core.Odin.ParseSchema("age = ## :(0..150)");
        var doc = Core.Odin.Parse("age = ##-1");
        var result = Core.Odin.Validate(doc, schema);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == ValidationErrorCode.ValueOutOfBounds);
    }

    [Fact]
    public void Validate_NumberAboveMaximum()
    {
        var schema = Core.Odin.ParseSchema("age = ## :(0..150)");
        var doc = Core.Odin.Parse("age = ##200");
        var result = Core.Odin.Validate(doc, schema);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == ValidationErrorCode.ValueOutOfBounds);
    }

    // ─────────────────────────────────────────────────────────────────
    // V004: Pattern Mismatch
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_PatternMatch()
    {
        var schema = Core.Odin.ParseSchema("code = :pattern \"^[A-Z]{3}$\"");
        var doc = Core.Odin.Parse("code = \"ABC\"");
        var result = Core.Odin.Validate(doc, schema);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_PatternNoMatch()
    {
        var schema = Core.Odin.ParseSchema("code = :pattern \"^[A-Z]{3}$\"");
        var doc = Core.Odin.Parse("code = \"abc\"");
        var result = Core.Odin.Validate(doc, schema);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == ValidationErrorCode.PatternMismatch);
    }

    // ─────────────────────────────────────────────────────────────────
    // V005: Invalid Enum Value
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_EnumValid()
    {
        var schema = Core.Odin.ParseSchema("status = :enum(active, inactive, pending)");
        var doc = Core.Odin.Parse("status = \"active\"");
        var result = Core.Odin.Validate(doc, schema);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_EnumInvalid()
    {
        var schema = Core.Odin.ParseSchema("status = :enum(active, inactive, pending)");
        var doc = Core.Odin.Parse("status = \"unknown\"");
        var result = Core.Odin.Validate(doc, schema);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == ValidationErrorCode.InvalidEnumValue);
    }

    // ─────────────────────────────────────────────────────────────────
    // V011: Unknown Fields (Strict Mode)
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_StrictMode_KnownFields_Valid()
    {
        var schema = Core.Odin.ParseSchema("name = string\nage = ##");
        var doc = Core.Odin.Parse("name = \"Alice\"\nage = ##30");
        var result = Core.Odin.Validate(doc, schema, new ValidateOptions { Strict = true });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_StrictMode_UnknownField_Error()
    {
        var schema = Core.Odin.ParseSchema("name = string");
        var doc = Core.Odin.Parse("name = \"Alice\"\nextra = ##42");
        var result = Core.Odin.Validate(doc, schema, new ValidateOptions { Strict = true });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == ValidationErrorCode.UnknownField);
    }

    [Fact]
    public void Validate_NonStrictMode_UnknownField_Valid()
    {
        var schema = Core.Odin.ParseSchema("name = string");
        var doc = Core.Odin.Parse("name = \"Alice\"\nextra = ##42");
        var result = Core.Odin.Validate(doc, schema);
        Assert.True(result.IsValid);
    }

    // ─────────────────────────────────────────────────────────────────
    // V012/V013: Reference Validation
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_ValidReference()
    {
        var schema = Core.Odin.ParseSchema("name = string");
        var doc = Core.Odin.Parse("name = \"Alice\"\nref = @name");
        var result = Core.Odin.Validate(doc, schema, new ValidateOptions { ValidateReferences = true });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_UnresolvedReference()
    {
        var schema = Core.Odin.ParseSchema("name = string");
        var doc = Core.Odin.Parse("name = \"Alice\"\nref = @missing");
        var result = Core.Odin.Validate(doc, schema, new ValidateOptions { ValidateReferences = true });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == ValidationErrorCode.UnresolvedReference);
    }

    [Fact]
    public void Validate_CircularReference()
    {
        var schema = Core.Odin.ParseSchema("");
        var doc = Core.Odin.Parse("a = @b\nb = @a");
        var result = Core.Odin.Validate(doc, schema, new ValidateOptions { ValidateReferences = true });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == ValidationErrorCode.CircularReference);
    }

    // ─────────────────────────────────────────────────────────────────
    // Format Validators
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_EmailFormat_Valid()
    {
        var schema = Core.Odin.ParseSchema("email = :format email");
        var doc = Core.Odin.Parse("email = \"user@example.com\"");
        var result = Core.Odin.Validate(doc, schema);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_EmailFormat_Invalid()
    {
        var schema = Core.Odin.ParseSchema("email = :format email");
        var doc = Core.Odin.Parse("email = \"not-an-email\"");
        var result = Core.Odin.Validate(doc, schema);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_UuidFormat_Valid()
    {
        var schema = Core.Odin.ParseSchema("id = :format uuid");
        var doc = Core.Odin.Parse("id = \"550e8400-e29b-41d4-a716-446655440000\"");
        var result = Core.Odin.Validate(doc, schema);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_UuidFormat_Invalid()
    {
        var schema = Core.Odin.ParseSchema("id = :format uuid");
        var doc = Core.Odin.Parse("id = \"not-a-uuid\"");
        var result = Core.Odin.Validate(doc, schema);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_Ipv4Format_Valid()
    {
        var schema = Core.Odin.ParseSchema("ip = :format ipv4");
        var doc = Core.Odin.Parse("ip = \"192.168.1.1\"");
        var result = Core.Odin.Validate(doc, schema);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Ipv4Format_Invalid()
    {
        var schema = Core.Odin.ParseSchema("ip = :format ipv4");
        var doc = Core.Odin.Parse("ip = \"999.999.999.999\"");
        var result = Core.Odin.Validate(doc, schema);
        Assert.False(result.IsValid);
    }

    // ─────────────────────────────────────────────────────────────────
    // Schema Type Definitions
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseSchema_TypeDefinition()
    {
        var schemaText = "{@Person}\nname = ! string\nage = ##";
        var schema = Core.Odin.ParseSchema(schemaText);
        Assert.True(schema.Types.ContainsKey("Person"));
        Assert.Equal(2, schema.Types["Person"].SchemaFields.Count);
    }

    [Fact]
    public void Validate_TypeDefinition_FieldsValidated()
    {
        var schemaText = "{@Person}\nname = ! string\nage = ##";
        var schema = Core.Odin.ParseSchema(schemaText);
        var doc = Core.Odin.Parse("{Person}\nname = \"Alice\"\nage = ##30");
        var result = Core.Odin.Validate(doc, schema);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_TypeDefinition_TypeMismatch()
    {
        var schemaText = "{@Person}\nname = string\nage = ##";
        var schema = Core.Odin.ParseSchema(schemaText);
        var doc = Core.Odin.Parse("{Person}\nname = \"Alice\"\nage = \"not_a_number\"");
        var result = Core.Odin.Validate(doc, schema);
        Assert.False(result.IsValid);
    }

    // ─────────────────────────────────────────────────────────────────
    // FailFast Option
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_FailFast_StopsAtFirstError()
    {
        var schema = Core.Odin.ParseSchema("a = ! string\nb = ! ##\nc = ! ?");
        var doc = Core.Odin.Parse("x = ##1"); // all 3 required fields missing
        var result = Core.Odin.Validate(doc, schema, new ValidateOptions { FailFast = true });
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void Validate_NoFailFast_ReportsAllErrors()
    {
        var schema = Core.Odin.ParseSchema("a = ! string\nb = ! ##\nc = ! ?");
        var doc = Core.Odin.Parse("x = ##1"); // all 3 required fields missing
        var result = Core.Odin.Validate(doc, schema, new ValidateOptions { FailFast = false });
        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 3);
    }

    // ─────────────────────────────────────────────────────────────────
    // ValidationResult and ValidationError
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ValidationResult_Valid_IsValid()
    {
        var result = ValidationResult.Valid();
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidationResult_WithErrors_IsNotValid()
    {
        var errors = new List<ValidationError>
        {
            new ValidationError(ValidationErrorCode.RequiredFieldMissing, "name", "Required field missing")
        };
        var result = ValidationResult.WithErrors(errors);
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void ValidationError_HasCorrectCode()
    {
        var error = new ValidationError(ValidationErrorCode.TypeMismatch, "age", "Expected integer");
        Assert.Equal("V002", error.Code);
        Assert.Equal("age", error.Path);
    }

    [Fact]
    public void ValidationError_ToString_IncludesCode()
    {
        var error = new ValidationError(ValidationErrorCode.RequiredFieldMissing, "name", "Required field missing");
        var str = error.ToString();
        Assert.Contains("V001", str);
        Assert.Contains("name", str);
    }

    // ─────────────────────────────────────────────────────────────────
    // Error Code Extensions
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseErrorCode_HasCorrectCodes()
    {
        Assert.Equal("P001", ParseErrorCode.UnexpectedCharacter.Code());
        Assert.Equal("P004", ParseErrorCode.UnterminatedString.Code());
        Assert.Equal("P007", ParseErrorCode.DuplicatePathAssignment.Code());
        Assert.Equal("P010", ParseErrorCode.MaximumDepthExceeded.Code());
    }

    [Fact]
    public void ValidationErrorCode_HasCorrectCodes()
    {
        Assert.Equal("V001", ValidationErrorCode.RequiredFieldMissing.Code());
        Assert.Equal("V002", ValidationErrorCode.TypeMismatch.Code());
        Assert.Equal("V003", ValidationErrorCode.ValueOutOfBounds.Code());
        Assert.Equal("V012", ValidationErrorCode.CircularReference.Code());
        Assert.Equal("V013", ValidationErrorCode.UnresolvedReference.Code());
    }

    [Fact]
    public void ParseFromCode_ReturnsCorrectEnum()
    {
        Assert.Equal(ParseErrorCode.UnexpectedCharacter, ErrorCodeExtensions.ParseFromCode("P001"));
        Assert.Equal(ParseErrorCode.UnterminatedString, ErrorCodeExtensions.ParseFromCode("P004"));
        Assert.Null(ErrorCodeExtensions.ParseFromCode("P999"));
    }

    [Fact]
    public void ValidationFromCode_ReturnsCorrectEnum()
    {
        Assert.Equal(ValidationErrorCode.RequiredFieldMissing, ErrorCodeExtensions.ValidationFromCode("V001"));
        Assert.Equal(ValidationErrorCode.TypeMismatch, ErrorCodeExtensions.ValidationFromCode("V002"));
        Assert.Null(ErrorCodeExtensions.ValidationFromCode("V999"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Schema Serialization Round-Trip
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void SerializeSchema_RoundTrip()
    {
        var schemaText = "name = ! string\nage = ##";
        var schema = Core.Odin.ParseSchema(schemaText);
        var serialized = Core.Odin.SerializeSchema(schema);
        Assert.NotNull(serialized);
        Assert.True(serialized.Length > 0);
    }

    // ─────────────────────────────────────────────────────────────────
    // Validate Optional Fields
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_OptionalField_Absent_Valid()
    {
        var schema = Core.Odin.ParseSchema("name = string");
        var doc = Core.Odin.Parse("age = ##30");
        var result = Core.Odin.Validate(doc, schema);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_OptionalField_Present_Valid()
    {
        var schema = Core.Odin.ParseSchema("name = string");
        var doc = Core.Odin.Parse("name = \"Alice\"");
        var result = Core.Odin.Validate(doc, schema);
        Assert.True(result.IsValid);
    }

    // ─────────────────────────────────────────────────────────────────
    // Integer as Number allowed
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_IntegerAcceptedAsNumber()
    {
        var schema = Core.Odin.ParseSchema("value = #");
        var doc = Core.Odin.Parse("value = ##42");
        var result = Core.Odin.Validate(doc, schema);
        Assert.True(result.IsValid);
    }

    // ─────────────────────────────────────────────────────────────────
    // Schema Comments
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseSchema_IgnoresComments()
    {
        var schema = Core.Odin.ParseSchema("; This is a schema\nname = ! string\n; age field\nage = ##");
        Assert.Equal(2, schema.Fields.Count);
    }

    // ─────────────────────────────────────────────────────────────────
    // Format Validation Convenience
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ValidateFormats_ValidEmail()
    {
        var doc = Core.Odin.Parse("email = \"user@example.com\"");
        var errors = ValidationEngine.ValidateFormats(doc, "email = :format email");
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateFormats_InvalidEmail()
    {
        var doc = Core.Odin.Parse("email = \"not-an-email\"");
        var errors = ValidationEngine.ValidateFormats(doc, "email = :format email");
        Assert.True(errors.Count > 0);
    }

    // ─────────────────────────────────────────────────────────────────
    // Multiple fields validation
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_AllFieldsValid()
    {
        var schema = Core.Odin.ParseSchema("name = string\nage = ##\nactive = ?");
        var doc = Core.Odin.Parse("name = \"Alice\"\nage = ##30\nactive = true");
        var result = Core.Odin.Validate(doc, schema);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_MultipleErrors()
    {
        var schema = Core.Odin.ParseSchema("name = ! string\nage = ! ##");
        var doc = Core.Odin.Parse("other = \"val\"");
        var result = Core.Odin.Validate(doc, schema);
        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 2);
    }

    [Fact]
    public void Validate_OptionalFieldMissing_IsValid()
    {
        var schema = Core.Odin.ParseSchema("name = ! string\nnickname = string");
        var doc = Core.Odin.Parse("name = \"Alice\"");
        var result = Core.Odin.Validate(doc, schema);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_NullValueForRequiredField()
    {
        var schema = Core.Odin.ParseSchema("name = ! string");
        var doc = Core.Odin.Parse("name = ~");
        var result = Core.Odin.Validate(doc, schema);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_BooleanField_AcceptsTrue()
    {
        var schema = Core.Odin.ParseSchema("flag = ?");
        var doc = Core.Odin.Parse("flag = true");
        var result = Core.Odin.Validate(doc, schema);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_BooleanField_AcceptsFalse()
    {
        var schema = Core.Odin.ParseSchema("flag = ?");
        var doc = Core.Odin.Parse("flag = false");
        var result = Core.Odin.Validate(doc, schema);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_CurrencyField()
    {
        var schema = Core.Odin.ParseSchema("price = #$");
        var doc = Core.Odin.Parse("price = #$99.99");
        var result = Core.Odin.Validate(doc, schema);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_PercentField()
    {
        var schema = Core.Odin.ParseSchema("rate = percent");
        var doc = Core.Odin.Parse("rate = #%50");
        var result = Core.Odin.Validate(doc, schema);
        Assert.True(result.IsValid);
    }
}
