using Toren.App.Documents.Models;

namespace Toren.App.Documents.Services;

public static class DocumentSessionStateComparer
{
    public static bool AreEquivalent(DocumentSessionState? left, DocumentSessionState? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null
            || !string.Equals(left.ActiveDocumentPath, right.ActiveDocumentPath, StringComparison.Ordinal)
            || !left.OpenDocumentPaths.SequenceEqual(right.OpenDocumentPaths, StringComparer.Ordinal))
        {
            return false;
        }

        var leftRecovery = left.RecoveryDocuments ?? [];
        var rightRecovery = right.RecoveryDocuments ?? [];
        if (leftRecovery.Length != rightRecovery.Length)
        {
            return false;
        }

        for (var index = 0; index < leftRecovery.Length; index++)
        {
            var leftSnapshot = leftRecovery[index];
            var rightSnapshot = rightRecovery[index];
            if (!string.Equals(leftSnapshot.Path, rightSnapshot.Path, StringComparison.Ordinal)
                || !string.Equals(leftSnapshot.Text, rightSnapshot.Text, StringComparison.Ordinal)
                || leftSnapshot.Encoding != rightSnapshot.Encoding)
            {
                return false;
            }
        }

        return true;
    }
}
