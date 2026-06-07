using System.Globalization;

namespace OpenIdentityStack.Application.ApplicationPermissions.Planning;

public static class ManifestVersionComparer
{
    public static int Compare(string left, string right)
    {
        var parsedLeft = ParsedSemVer.Parse(left);
        var parsedRight = ParsedSemVer.Parse(right);

        int coreComparison = parsedLeft.Major != parsedRight.Major
            ? parsedLeft.Major.CompareTo(parsedRight.Major)
            : parsedLeft.Minor != parsedRight.Minor
                ? parsedLeft.Minor.CompareTo(parsedRight.Minor)
                : parsedLeft.Patch.CompareTo(parsedRight.Patch);
        if (coreComparison != 0)
        {
            return coreComparison;
        }

        if (parsedLeft.Prerelease is null && parsedRight.Prerelease is null)
        {
            return 0;
        }

        if (parsedLeft.Prerelease is null)
        {
            return 1;
        }

        if (parsedRight.Prerelease is null)
        {
            return -1;
        }

        return ComparePreRelease(parsedLeft.Prerelease, parsedRight.Prerelease);
    }

    private static int ComparePreRelease(string left, string right)
    {
        string[] parsedLeft = left.Split('.', StringSplitOptions.None);
        string[] parsedRight = right.Split('.', StringSplitOptions.None);

        int maxLength = Math.Min(parsedLeft.Length, parsedRight.Length);
        for (int i = 0; i < maxLength; i++)
        {
            int segmentComparison = CompareIdentifier(parsedLeft[i], parsedRight[i]);
            if (segmentComparison != 0)
            {
                return segmentComparison;
            }
        }

        return parsedLeft.Length.CompareTo(parsedRight.Length);
    }

    private static int CompareIdentifier(string left, string right)
    {
        bool leftIsNumeric = int.TryParse(left, NumberStyles.None, CultureInfo.InvariantCulture, out int leftValue);
        bool rightIsNumeric = int.TryParse(right, NumberStyles.None, CultureInfo.InvariantCulture, out int rightValue);

        if (leftIsNumeric && rightIsNumeric)
        {
            return leftValue.CompareTo(rightValue);
        }

        if (leftIsNumeric)
        {
            return -1;
        }

        if (rightIsNumeric)
        {
            return 1;
        }

        return string.Compare(left, right, StringComparison.Ordinal);
    }

    private readonly record struct ParsedSemVer(int Major, int Minor, int Patch, string? Prerelease)
    {
        public static ParsedSemVer Parse(string value)
        {
            string[] versionAndPrerelease = value.Split('-', 2);
            string[] core = versionAndPrerelease[0].Split('.');
            return new ParsedSemVer(
                int.Parse(core[0], CultureInfo.InvariantCulture),
                int.Parse(core[1], CultureInfo.InvariantCulture),
                int.Parse(core[2], CultureInfo.InvariantCulture),
                versionAndPrerelease.Length == 2 ? versionAndPrerelease[1] : null);
        }
    }
}
