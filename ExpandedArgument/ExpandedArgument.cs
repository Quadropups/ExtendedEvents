using System;
using UnityEngine;

namespace ExtendedEvents {
    /// <summary>
    /// A serializable Argument that also supports serialization of <see cref="Gradient"/> and <see cref="AnimationCurve"/> types.
    /// </summary>
    [Serializable]
    public class ExpandedArgument : Argument {
        #region Fields

        /// <summary>Gradient argument</summary>
        [SerializeField] [GradientUsage(true)] private Gradient _gradientArgument;

        /// <summary>AnimationCurve argument</summary>
        [SerializeField] private AnimationCurve _animationCurveArgument;

        #endregion

        /// <summary>
        /// This method will be used by ExtendedEvent to get serialized <see cref="AnimationCurve"/> argument. 
        /// <para></para>
        /// Value is retrieved through casting this instance to <see cref="AnimationCurve"/> type using <see cref="TypeCaster{T, TResult}"/> 
        /// </summary>
        [TypeCaster] public AnimationCurve GetAnimationCurve() => _animationCurveArgument;

        /// <summary>
        /// This method will be used by ExtendedEvent to get serialized <see cref="Gradient"/> argument. 
        /// <para></para>
        /// Value is retrieved through casting this instance to <see cref="Gradient"/> type using <see cref="TypeCaster{T, TResult}"/> 
        /// </summary>
        [TypeCaster] public Gradient GetGradient() => _gradientArgument;
    }
}
