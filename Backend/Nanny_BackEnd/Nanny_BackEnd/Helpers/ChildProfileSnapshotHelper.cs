using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Helpers;

public static class ChildProfileSnapshotHelper
{
    public sealed class ChildSnapshot
    {
        public string? Characteristic { get; init; }
        public int? BirthType { get; init; }
        public string? SpecialNeeds { get; init; }
    }

    public static List<ChildProfile> ResolveChildren(
        ParentProfile? parentProfile,
        Guid? primaryChildId,
        int? numberOfChildren)
    {
        var activeChildren = parentProfile?.ChildProfiles
            .Where(child => !child.IsDeleted)
            .OrderBy(child => child.CreatedAt)
            .ToList() ?? [];

        if (activeChildren.Count == 0)
            return [];

        var requestedCount = Math.Max(1, numberOfChildren ?? 1);
        var takeCount = Math.Min(requestedCount, activeChildren.Count);

        var startIndex = 0;
        if (primaryChildId.HasValue)
        {
            var foundIndex = activeChildren.FindIndex(child => child.Id == primaryChildId.Value);
            if (foundIndex >= 0)
                startIndex = foundIndex;
        }

        var resolved = new List<ChildProfile>(takeCount);
        for (var offset = 0; offset < takeCount; offset += 1)
        {
            var index = (startIndex + offset) % activeChildren.Count;
            resolved.Add(activeChildren[index]);
        }

        return resolved;
    }

    public static ChildSnapshot BuildSnapshot(IEnumerable<ChildProfile> children, string? fallbackCharacteristic = null)
    {
        var childList = (children ?? Enumerable.Empty<ChildProfile>()).ToList();

        var characteristicValues = childList
            .Select(child => child.Characteristic?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (characteristicValues.Count == 0 && !string.IsNullOrWhiteSpace(fallbackCharacteristic))
            characteristicValues.Add(fallbackCharacteristic.Trim());

        var birthTypeValues = childList
            .Where(child => child.ChildAgeGroup.HasValue)
            .Select(child => (int)child.ChildAgeGroup!.Value)
            .Distinct()
            .ToList();

        var specialNeedValues = childList
            .SelectMany(child => SplitSpecialNeeds(child.SpecialNeeds))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ChildSnapshot
        {
            Characteristic = characteristicValues.Count > 0 ? string.Join("; ", characteristicValues) : null,
            BirthType = birthTypeValues.Count == 1 ? birthTypeValues[0] : null,
            SpecialNeeds = specialNeedValues.Count > 0 ? string.Join(", ", specialNeedValues) : null
        };
    }

    private static IEnumerable<string> SplitSpecialNeeds(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return [];

        return rawValue
            .Split([',', ';', '|', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value));
    }
}
