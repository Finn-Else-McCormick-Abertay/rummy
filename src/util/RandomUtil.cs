using System;
using System.Collections.Generic;
using System.Linq;

namespace Rummy.Util;

static class RandomExtensions
{
    /// <summary>Random roll with (<paramref name="chance"/>) chance of success. Chance is in the form of a double from 0.0 to 1.0 </summary>
    /// <param name="chance">Percentage chance between 0.0 (0%) and 1.0 (100%)</param>
    /// <returns></returns>
    public static bool Roll(this Random random, double chance) => random.NextDouble() < chance;

    public static T From<T>(this Random random, params IEnumerable<T> args) => args.ElementAt(random.Next(args.Count()));
}