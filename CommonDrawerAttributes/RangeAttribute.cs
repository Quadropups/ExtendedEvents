using System;
using UnityEngine;

namespace ExtendedEvents {

    /// <summary>Attribute used to make a float or int field or parameter be restricted to a specific range.</summary>
    public class RangeAttribute : ExtendedEventPropertyAttribute {
        public readonly float min;
        public readonly float max;

        // Attribute used to make a float or int variable in a script be restricted to a specific range.
        public RangeAttribute(float min, float max) {
            this.min = min;
            this.max = max;
        }
    }
}