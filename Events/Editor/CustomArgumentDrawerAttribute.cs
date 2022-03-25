namespace ExtendedEvents {
    using System;

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    /// <summary>Tells ExtendedEventDrawer which method parameter or type to use this PropertyDrawer or ArgumentDrawer for</summary>
    public sealed class CustomParameterDrawer : Attribute {
        #region Fields

        private readonly bool m_IsParameter;

        private readonly Type m_Type;

        private readonly bool m_UseForChildren;

        private readonly string m_MethodName;

        private readonly Type m_ParameterType;

        private readonly string m_ParameterName;

        #endregion

        /// <summary>Tells a PropertyDrawer class which method parameter it's a drawer for.</summary>
        public CustomParameterDrawer(Type declaringType, string methodName, string parameterName) {
            m_Type = declaringType;
            m_MethodName = methodName;
            //m_ParameterType = parameterType;
            m_ParameterName = parameterName;

            m_IsParameter = true;
        }

        /// <summary>Tells a PropertyDrawer class which method parameter it's a drawer for.</summary>
        public CustomParameterDrawer(Type declaringType, string methodName, Type parameterType, string parameterName) {
            m_Type = declaringType;
            m_MethodName = methodName;
            m_ParameterType = parameterType;
            m_ParameterName = parameterName;

            m_IsParameter = true;
        }

        /// <summary>Tells a PropertyDrawer class which method parameter it's a drawer for.</summary>
        public CustomParameterDrawer(Type declaringType, string parameterOrPropertyName) {
            m_Type = declaringType;
            //m_MethodName = methodName;
            //m_ParameterType = parameterType;
            m_ParameterName = parameterOrPropertyName;

            m_IsParameter = true;
        }

        /// <summary>Tells a PropertyDrawer class which method parameter it's a drawer for.</summary>
        public CustomParameterDrawer(Type declaringType, Type parameterType) {
            m_Type = declaringType;
            //m_MethodName = methodName;
            m_ParameterType = parameterType;
            //m_ParameterName = parameterName;

            m_IsParameter = true;
        }

        /// <summary>Tells a PropertyDrawer class which method parameter it's a drawer for.</summary>
        public CustomParameterDrawer(Type declaringType, Type parameterType, string parameterName) {
            m_Type = declaringType;
            //m_MethodName = methodName;
            m_ParameterType = parameterType;
            m_ParameterName = parameterName;

            m_IsParameter = true;
        }

        /// <summary>Tells a PropertyDrawer class which run-time class or attribute it's a drawer for when it's used as an argument in a method or setter.</summary>
        public CustomParameterDrawer(Type type) {
            m_Type = type;
        }

        /// <summary>Tells a PropertyDrawer class which run-time class or attribute it's a drawer for when it's used as an argument in a method or setter.</summary>
        public CustomParameterDrawer(Type type, bool useForChildren) {
            m_Type = type;
            m_UseForChildren = useForChildren;
        }

        public bool isParameter => m_IsParameter;

        public string methodName => m_MethodName;

        public string parameterName => m_ParameterName;

        public Type parameterType => m_ParameterType;

        public Type type => m_Type;

        public bool useForChildren => m_UseForChildren;
    }
}
