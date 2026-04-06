using System;
using System.Collections.Generic;

namespace Odin.Core.Types;

/// <summary>Parse error codes (P001-P015). API contract — identical across all SDKs.</summary>
public enum ParseErrorCode
{
    /// <summary>P001: Unexpected character.</summary>
    UnexpectedCharacter,
    /// <summary>P002: Bare (unquoted) string values are not allowed.</summary>
    BareStringNotAllowed,
    /// <summary>P003: Invalid array index.</summary>
    InvalidArrayIndex,
    /// <summary>P004: Unterminated string.</summary>
    UnterminatedString,
    /// <summary>P005: Invalid escape sequence.</summary>
    InvalidEscapeSequence,
    /// <summary>P006: Invalid type prefix.</summary>
    InvalidTypePrefix,
    /// <summary>P007: Duplicate path assignment.</summary>
    DuplicatePathAssignment,
    /// <summary>P008: Invalid header syntax.</summary>
    InvalidHeaderSyntax,
    /// <summary>P009: Invalid directive syntax.</summary>
    InvalidDirective,
    /// <summary>P010: Maximum nesting depth exceeded.</summary>
    MaximumDepthExceeded,
    /// <summary>P011: Maximum document size exceeded.</summary>
    MaximumDocumentSizeExceeded,
    /// <summary>P012: Invalid UTF-8 sequence.</summary>
    InvalidUtf8Sequence,
    /// <summary>P013: Non-contiguous array indices.</summary>
    NonContiguousArrayIndices,
    /// <summary>P014: Empty document.</summary>
    EmptyDocument,
    /// <summary>P015: Array index out of range.</summary>
    ArrayIndexOutOfRange,
}

/// <summary>Validation error codes (V001-V013). API contract — identical across all SDKs.</summary>
public enum ValidationErrorCode
{
    /// <summary>V001: Required field missing.</summary>
    RequiredFieldMissing,
    /// <summary>V002: Type mismatch.</summary>
    TypeMismatch,
    /// <summary>V003: Value out of bounds.</summary>
    ValueOutOfBounds,
    /// <summary>V004: Pattern mismatch.</summary>
    PatternMismatch,
    /// <summary>V005: Invalid enum value.</summary>
    InvalidEnumValue,
    /// <summary>V006: Array length violation.</summary>
    ArrayLengthViolation,
    /// <summary>V007: Unique constraint violation.</summary>
    UniqueConstraintViolation,
    /// <summary>V008: Invariant violation.</summary>
    InvariantViolation,
    /// <summary>V009: Cardinality constraint violation.</summary>
    CardinalityConstraintViolation,
    /// <summary>V010: Conditional requirement not met.</summary>
    ConditionalRequirementNotMet,
    /// <summary>V011: Unknown field in strict mode.</summary>
    UnknownField,
    /// <summary>V012: Circular reference.</summary>
    CircularReference,
    /// <summary>V013: Unresolved reference.</summary>
    UnresolvedReference,
}

/// <summary>Transform error codes (T001-T011). API contract — identical across all SDKs.</summary>
public enum TransformErrorCode
{
    /// <summary>T001: Unknown verb — the specified verb does not exist.</summary>
    UnknownVerb,
    /// <summary>T002: Invalid verb arguments — wrong number or type of arguments.</summary>
    InvalidVerbArgs,
    /// <summary>T003: Lookup table not found — referenced table doesn't exist.</summary>
    LookupTableNotFound,
    /// <summary>T004: Lookup key not found — key doesn't exist in table.</summary>
    LookupKeyNotFound,
    /// <summary>T005: Source path not found — cannot resolve source path.</summary>
    SourcePathNotFound,
    /// <summary>T006: Invalid output format — unsupported or misconfigured format.</summary>
    InvalidOutputFormat,
    /// <summary>T007: Invalid modifier — modifier not applicable to target format.</summary>
    InvalidModifier,
    /// <summary>T008: Accumulator overflow — accumulator value exceeds limits.</summary>
    AccumulatorOverflow,
    /// <summary>T009: Loop source not array — :loop directive target is not an array.</summary>
    LoopSourceNotArray,
    /// <summary>T010: Position overflow — fixed-width field extends past line.</summary>
    PositionOverflow,
    /// <summary>T011: Incompatible conversion — incompatible or unknown conversion target.</summary>
    IncompatibleConversion,
}

/// <summary>Extension methods for error codes.</summary>
public static class ErrorCodeExtensions
{
    /// <summary>Returns the string code (e.g., "P001").</summary>
    public static string Code(this ParseErrorCode code) => code switch
    {
        ParseErrorCode.UnexpectedCharacter => "P001",
        ParseErrorCode.BareStringNotAllowed => "P002",
        ParseErrorCode.InvalidArrayIndex => "P003",
        ParseErrorCode.UnterminatedString => "P004",
        ParseErrorCode.InvalidEscapeSequence => "P005",
        ParseErrorCode.InvalidTypePrefix => "P006",
        ParseErrorCode.DuplicatePathAssignment => "P007",
        ParseErrorCode.InvalidHeaderSyntax => "P008",
        ParseErrorCode.InvalidDirective => "P009",
        ParseErrorCode.MaximumDepthExceeded => "P010",
        ParseErrorCode.MaximumDocumentSizeExceeded => "P011",
        ParseErrorCode.InvalidUtf8Sequence => "P012",
        ParseErrorCode.NonContiguousArrayIndices => "P013",
        ParseErrorCode.EmptyDocument => "P014",
        ParseErrorCode.ArrayIndexOutOfRange => "P015",
        _ => "P000",
    };

    /// <summary>Returns the default message for this error code.</summary>
    public static string Message(this ParseErrorCode code) => code switch
    {
        ParseErrorCode.UnexpectedCharacter => "Unexpected character",
        ParseErrorCode.BareStringNotAllowed => "Strings must be quoted",
        ParseErrorCode.InvalidArrayIndex => "Invalid array index",
        ParseErrorCode.UnterminatedString => "Unterminated string",
        ParseErrorCode.InvalidEscapeSequence => "Invalid escape sequence",
        ParseErrorCode.InvalidTypePrefix => "Invalid type prefix",
        ParseErrorCode.DuplicatePathAssignment => "Duplicate path assignment",
        ParseErrorCode.InvalidHeaderSyntax => "Invalid header syntax",
        ParseErrorCode.InvalidDirective => "Invalid directive",
        ParseErrorCode.MaximumDepthExceeded => "Maximum depth exceeded",
        ParseErrorCode.MaximumDocumentSizeExceeded => "Maximum document size exceeded",
        ParseErrorCode.InvalidUtf8Sequence => "Invalid UTF-8 sequence",
        ParseErrorCode.NonContiguousArrayIndices => "Non-contiguous array indices",
        ParseErrorCode.EmptyDocument => "Empty document",
        ParseErrorCode.ArrayIndexOutOfRange => "Array index out of range",
        _ => "Unknown error",
    };

    /// <summary>Parse from a code string like "P001".</summary>
    public static ParseErrorCode? ParseFromCode(string code) => code switch
    {
        "P001" => ParseErrorCode.UnexpectedCharacter,
        "P002" => ParseErrorCode.BareStringNotAllowed,
        "P003" => ParseErrorCode.InvalidArrayIndex,
        "P004" => ParseErrorCode.UnterminatedString,
        "P005" => ParseErrorCode.InvalidEscapeSequence,
        "P006" => ParseErrorCode.InvalidTypePrefix,
        "P007" => ParseErrorCode.DuplicatePathAssignment,
        "P008" => ParseErrorCode.InvalidHeaderSyntax,
        "P009" => ParseErrorCode.InvalidDirective,
        "P010" => ParseErrorCode.MaximumDepthExceeded,
        "P011" => ParseErrorCode.MaximumDocumentSizeExceeded,
        "P012" => ParseErrorCode.InvalidUtf8Sequence,
        "P013" => ParseErrorCode.NonContiguousArrayIndices,
        "P014" => ParseErrorCode.EmptyDocument,
        "P015" => ParseErrorCode.ArrayIndexOutOfRange,
        _ => null,
    };

    /// <summary>Returns the string code (e.g., "V001").</summary>
    public static string Code(this ValidationErrorCode code) => code switch
    {
        ValidationErrorCode.RequiredFieldMissing => "V001",
        ValidationErrorCode.TypeMismatch => "V002",
        ValidationErrorCode.ValueOutOfBounds => "V003",
        ValidationErrorCode.PatternMismatch => "V004",
        ValidationErrorCode.InvalidEnumValue => "V005",
        ValidationErrorCode.ArrayLengthViolation => "V006",
        ValidationErrorCode.UniqueConstraintViolation => "V007",
        ValidationErrorCode.InvariantViolation => "V008",
        ValidationErrorCode.CardinalityConstraintViolation => "V009",
        ValidationErrorCode.ConditionalRequirementNotMet => "V010",
        ValidationErrorCode.UnknownField => "V011",
        ValidationErrorCode.CircularReference => "V012",
        ValidationErrorCode.UnresolvedReference => "V013",
        _ => "V000",
    };

    /// <summary>Returns the default message for this error code.</summary>
    public static string Message(this ValidationErrorCode code) => code switch
    {
        ValidationErrorCode.RequiredFieldMissing => "Required field missing",
        ValidationErrorCode.TypeMismatch => "Type mismatch",
        ValidationErrorCode.ValueOutOfBounds => "Value out of bounds",
        ValidationErrorCode.PatternMismatch => "Pattern mismatch",
        ValidationErrorCode.InvalidEnumValue => "Invalid enum value",
        ValidationErrorCode.ArrayLengthViolation => "Array length violation",
        ValidationErrorCode.UniqueConstraintViolation => "Unique constraint violation",
        ValidationErrorCode.InvariantViolation => "Invariant violation",
        ValidationErrorCode.CardinalityConstraintViolation => "Cardinality constraint violation",
        ValidationErrorCode.ConditionalRequirementNotMet => "Conditional requirement not met",
        ValidationErrorCode.UnknownField => "Unknown field",
        ValidationErrorCode.CircularReference => "Circular reference",
        ValidationErrorCode.UnresolvedReference => "Unresolved reference",
        _ => "Unknown error",
    };

    /// <summary>Parse from a code string like "V001".</summary>
    public static ValidationErrorCode? ValidationFromCode(string code) => code switch
    {
        "V001" => ValidationErrorCode.RequiredFieldMissing,
        "V002" => ValidationErrorCode.TypeMismatch,
        "V003" => ValidationErrorCode.ValueOutOfBounds,
        "V004" => ValidationErrorCode.PatternMismatch,
        "V005" => ValidationErrorCode.InvalidEnumValue,
        "V006" => ValidationErrorCode.ArrayLengthViolation,
        "V007" => ValidationErrorCode.UniqueConstraintViolation,
        "V008" => ValidationErrorCode.InvariantViolation,
        "V009" => ValidationErrorCode.CardinalityConstraintViolation,
        "V010" => ValidationErrorCode.ConditionalRequirementNotMet,
        "V011" => ValidationErrorCode.UnknownField,
        "V012" => ValidationErrorCode.CircularReference,
        "V013" => ValidationErrorCode.UnresolvedReference,
        _ => null,
    };

    /// <summary>Returns the string code (e.g., "T001").</summary>
    public static string Code(this TransformErrorCode code) => code switch
    {
        TransformErrorCode.UnknownVerb => "T001",
        TransformErrorCode.InvalidVerbArgs => "T002",
        TransformErrorCode.LookupTableNotFound => "T003",
        TransformErrorCode.LookupKeyNotFound => "T004",
        TransformErrorCode.SourcePathNotFound => "T005",
        TransformErrorCode.InvalidOutputFormat => "T006",
        TransformErrorCode.InvalidModifier => "T007",
        TransformErrorCode.AccumulatorOverflow => "T008",
        TransformErrorCode.LoopSourceNotArray => "T009",
        TransformErrorCode.PositionOverflow => "T010",
        TransformErrorCode.IncompatibleConversion => "T011",
        _ => "T000",
    };

    /// <summary>Returns the default message for this error code.</summary>
    public static string Message(this TransformErrorCode code) => code switch
    {
        TransformErrorCode.UnknownVerb => "Unknown verb",
        TransformErrorCode.InvalidVerbArgs => "Invalid verb arguments",
        TransformErrorCode.LookupTableNotFound => "Lookup table not found",
        TransformErrorCode.LookupKeyNotFound => "Lookup key not found",
        TransformErrorCode.SourcePathNotFound => "Source path not found",
        TransformErrorCode.InvalidOutputFormat => "Invalid output format",
        TransformErrorCode.InvalidModifier => "Invalid modifier",
        TransformErrorCode.AccumulatorOverflow => "Accumulator overflow",
        TransformErrorCode.LoopSourceNotArray => "Loop source not array",
        TransformErrorCode.PositionOverflow => "Position overflow",
        TransformErrorCode.IncompatibleConversion => "Incompatible conversion",
        _ => "Unknown error",
    };

    /// <summary>Parse from a code string like "T001".</summary>
    public static TransformErrorCode? TransformFromCode(string code) => code switch
    {
        "T001" => TransformErrorCode.UnknownVerb,
        "T002" => TransformErrorCode.InvalidVerbArgs,
        "T003" => TransformErrorCode.LookupTableNotFound,
        "T004" => TransformErrorCode.LookupKeyNotFound,
        "T005" => TransformErrorCode.SourcePathNotFound,
        "T006" => TransformErrorCode.InvalidOutputFormat,
        "T007" => TransformErrorCode.InvalidModifier,
        "T008" => TransformErrorCode.AccumulatorOverflow,
        "T009" => TransformErrorCode.LoopSourceNotArray,
        "T010" => TransformErrorCode.PositionOverflow,
        "T011" => TransformErrorCode.IncompatibleConversion,
        _ => null,
    };
}

/// <summary>Exception thrown during ODIN text parsing.</summary>
public class OdinParseException : Exception
{
    /// <summary>Parse error code.</summary>
    public ParseErrorCode ErrorCode { get; }

    /// <summary>Line number (1-based) where the error occurred.</summary>
    public int Line { get; }

    /// <summary>Column number (1-based) where the error occurred.</summary>
    public int Column { get; }

    /// <summary>Creates a new parse exception.</summary>
    public OdinParseException(ParseErrorCode errorCode, int line, int column)
        : base($"{errorCode.Message()} at line {line}, column {column}")
    {
        ErrorCode = errorCode;
        Line = line;
        Column = column;
    }

    /// <summary>Creates a parse exception with a custom detail message.</summary>
    public OdinParseException(ParseErrorCode errorCode, int line, int column, string detail)
        : base($"{errorCode.Message()}: {detail} at line {line}, column {column}")
    {
        ErrorCode = errorCode;
        Line = line;
        Column = column;
    }

    /// <summary>Returns the string error code (e.g., "P001").</summary>
    public string Code => ErrorCode.Code();
}

/// <summary>A single validation error with path and details.</summary>
public sealed class ValidationError
{
    /// <summary>Path to the field with the error.</summary>
    public string Path { get; }

    /// <summary>Validation error code.</summary>
    public ValidationErrorCode ErrorCode { get; }

    /// <summary>Human-readable error message.</summary>
    public string ErrorMessage { get; }

    /// <summary>Expected value or type (for diagnostics).</summary>
    public string? Expected { get; init; }

    /// <summary>Actual value or type found (for diagnostics).</summary>
    public string? Actual { get; init; }

    /// <summary>Path in the schema that triggered this error.</summary>
    public string? SchemaPath { get; init; }

    /// <summary>Creates a new validation error.</summary>
    public ValidationError(ValidationErrorCode errorCode, string path, string message)
    {
        Path = path;
        ErrorCode = errorCode;
        ErrorMessage = message;
    }

    /// <summary>Returns the string error code (e.g., "V001").</summary>
    public string Code => ErrorCode.Code();

    /// <inheritdoc/>
    public override string ToString() => $"[{Code}] {ErrorMessage} at '{Path}'";
}

/// <summary>Result of validation — either valid or contains a list of errors.</summary>
public sealed class ValidationResult
{
    /// <summary>Whether the document is valid.</summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>List of validation errors (empty if valid).</summary>
    public IReadOnlyList<ValidationError> Errors { get; }

    /// <summary>Creates a successful validation result.</summary>
    public static ValidationResult Valid() => new(Array.Empty<ValidationError>());

    /// <summary>Creates a validation result with errors.</summary>
    public static ValidationResult WithErrors(IReadOnlyList<ValidationError> errors) => new(errors);

    private ValidationResult(IReadOnlyList<ValidationError> errors)
    {
        Errors = errors;
    }
}

/// <summary>Error during document patching.</summary>
public sealed class PatchError
{
    /// <summary>Human-readable error message.</summary>
    public string ErrorMessage { get; }

    /// <summary>Path where the error occurred.</summary>
    public string Path { get; }

    /// <summary>Creates a new patch error.</summary>
    public PatchError(string message, string path)
    {
        ErrorMessage = message;
        Path = path;
    }

    /// <inheritdoc/>
    public override string ToString() => $"Patch error at '{Path}': {ErrorMessage}";
}
