using System;
using Odin.Core.Types;

namespace Odin.Core.Diff;

/// <summary>
/// Applies an <see cref="OdinDiff"/> to an <see cref="OdinDocument"/>,
/// producing a new document with the changes applied.
/// </summary>
public static class Patcher
{
    /// <summary>
    /// Apply a diff to a document, producing a new document.
    /// </summary>
    /// <param name="doc">The base document to patch.</param>
    /// <param name="diff">The diff to apply.</param>
    /// <returns>A new <see cref="OdinDocument"/> with changes applied.</returns>
    /// <exception cref="OdinPatchException">
    /// Thrown if a path to be changed, removed, or moved does not exist in the document.
    /// </exception>
    public static OdinDocument Apply(OdinDocument doc, OdinDiff diff)
    {
        // Clone assignments so we can mutate
        var assignments = doc.Assignments.Clone();

        // Apply removals
        foreach (var removal in diff.Removed)
        {
            if (!assignments.ContainsKey(removal.Path))
            {
                throw new OdinPatchException(
                    new PatchError("path does not exist for removal", removal.Path));
            }
            assignments.Remove(removal.Path);
        }

        // Apply changes
        foreach (var change in diff.Changed)
        {
            if (!assignments.ContainsKey(change.Path))
            {
                throw new OdinPatchException(
                    new PatchError("path does not exist for change", change.Path));
            }
            assignments.Set(change.Path, change.NewValue);
        }

        // Apply additions
        foreach (var addition in diff.Added)
        {
            assignments.Set(addition.Path, addition.Value);
        }

        // Apply moves
        foreach (var mv in diff.Moved)
        {
            if (!assignments.TryGetValue(mv.FromPath, out var val))
            {
                throw new OdinPatchException(
                    new PatchError("source path does not exist for move", mv.FromPath));
            }
            assignments.Remove(mv.FromPath);
            assignments.Set(mv.ToPath, val);
        }

        return new OdinDocument(
            metadata: doc.Metadata,
            assignments: assignments,
            modifiers: doc.PathModifiers,
            imports: doc.Imports,
            schemas: doc.Schemas,
            conditionals: doc.Conditionals,
            comments: doc.Comments);
    }
}

/// <summary>
/// Exception thrown when a patch operation fails.
/// </summary>
public class OdinPatchException : Exception
{
    /// <summary>The underlying patch error with path and message details.</summary>
    public PatchError Error { get; }

    /// <summary>Creates a new patch exception.</summary>
    /// <param name="error">The patch error that caused this exception.</param>
    public OdinPatchException(PatchError error)
        : base(error.ToString())
    {
        Error = error;
    }
}
