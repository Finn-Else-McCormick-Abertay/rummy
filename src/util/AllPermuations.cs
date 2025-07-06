using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Rummy.Util;

public static class AllPermutations {
    public static IEnumerable<IEnumerable<T>> Of<T>(params IEnumerable<T> arr) {
        int arrayLength = arr.Count();
        if (arrayLength <= 1) return [arr];

        List<IEnumerable<T>> permutations = [];

        foreach (var (index, item) in arr.Index()) {
            IEnumerable<T> otherItems = arr.Where((x, i) => i != index);
            var otherPermutations = AllPermutations.Of(otherItems);

            for (int i = 0; i < arrayLength; ++i) {
                foreach (var permutation in otherPermutations) {
                    var workingPermutation = permutation.Select(x => x).ToList();
                    workingPermutation.Insert(i, item);
                    permutations.Add(workingPermutation);
                }
            }
        }

        // Distinct permutations
        return permutations.Where((x, i) => !permutations.Skip(i + 1).Any(y => Enumerable.SequenceEqual(x, y)));
    }
}