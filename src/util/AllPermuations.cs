using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

namespace Rummy.Util;

public static class AllPermutations {
    public static IEnumerable<IEnumerable<T>> Of<T>(params IEnumerable<T> arr) {
        int arrayLength = arr.Count();
        if (arrayLength <= 1) return [arr];

        System.Collections.Concurrent.ConcurrentQueue<IEnumerable<T>> permutations = [];

        Parallel.For(0, arr.Count(), arrIndex => {
            var otherPermutations = AllPermutations.Of(arr.Where((x, i) => i != arrIndex));

            Parallel.For(0, arrayLength, i =>
                Parallel.ForEach(otherPermutations, permutation => {
                    var workingPermutation = permutation.Select(x => x).ToList();
                    workingPermutation.Insert(i, arr.ElementAt(arrIndex));
                    permutations.Enqueue(workingPermutation);
                }));
        });

        // Distinct permutations
        return permutations.Where((x, i) => !permutations.Skip(i + 1).Any(y => Enumerable.SequenceEqual(x, y)));
    }
}