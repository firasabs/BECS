using System;
using Microsoft.ML.Transforms;

namespace Becs.ML
{
    public sealed class CondMapIn  { public string? Conditions_Csv { get; set; } }
    public sealed class CondMapOut { public float  CondFlag       { get; set; } }

    // Fully-qualified attribute name:
    [Microsoft.ML.Transforms.CustomMappingFactoryAttribute("CondFlagMap")]
    public sealed class CondFlagMap : CustomMappingFactory<CondMapIn, CondMapOut>
    {
        public override Action<CondMapIn, CondMapOut> GetMapping() => (src, dst) =>
        {
            var s = src.Conditions_Csv?.Trim();
            dst.CondFlag = (string.IsNullOrEmpty(s) || s == "[]" || s == "[ ]") ? 0f : 1f;
        };
    }
}