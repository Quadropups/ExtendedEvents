using System.Collections.Generic;
using UnityEngine;
using System;
using ExtendedEvents;

/// This is a partial class so that the user could define casters in their own script
public static partial class GlobalTypeCasters {

    #region Primitives

    /// <summary>
    /// Because "(float)int" operator is not defined on <see cref="float"/>, TypeCaster will not automatically use it as the default caster. So we have to define it manually.
    /// </summary>
    public static float CastIntToFloat(int value) => value;

    /// <summary>
    /// Because "(int)float" operator is not defined on <see cref="int"/>, TypeCaster will not automatically use it as the default caster. So we have to define it manually.
    /// </summary>
    public static int CastFloatToInt(float value) => (int)value;

    #endregion

    #region Unity

    /// <summary>
    /// Since all components have <see cref="Transform"/> attached to them, it's safe to cast them to <see cref="Transform"/>
    /// </summary>
    /// This Method, however, will not be used to cast <see cref="Transform"/> to <see cref="Component"/>, <see cref="Object"/> or <see cref="object"/>. 
    /// If Transform is to be cast to one of it's parent classes, the value will simply be returned as is.
    /// This is because before attempting to use any user defined casting method, <see cref="TypeCaster{T, TResult}"/> tries to simply return value as the type it inherits from (or implements in case of casting to interfaces)
    public static Transform GetTransform(Component value) => value.transform;

    /// <summary>
    /// Since all components are attached to a <see cref="GameObject"/>, it's safe to cast them to <see cref="GameObject"/>
    /// </summary>
    public static GameObject GetGameObject(Component value) => value.gameObject;

    /// <summary>
    /// Since <see cref="Collision"/> has collider property, it's sensible to use it as cast method
    /// </summary>
    public static Collider GetCollisionCollider(Collision value) => value.collider;

    /// <summary>
    /// Since <see cref="Renderer"/> has material property, it's sensible to use it as cast method
    /// </summary>
    public static Material GetRendererMaterial(Renderer value) => value.material;

    #endregion

    #region CachedData

    public static Action GetActionDelegate(CachedData data) => data.Invoke;

    public static Action<TArg> GetActionDelegate<TArg>(CachedData data) => data.Invoke<TArg>;

    public static Func<TResult> GetFuncDelegate<TResult>(CachedData data) => data.GetValue<TResult>;

    public static Func<TArg, TResult> GetFuncDelegate<TArg, TResult>(CachedData data) => data.GetValue<TArg, TResult>;

    #endregion
}
