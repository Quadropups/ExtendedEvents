using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ExtendedEvents {
    public abstract class CachedData<T> : CachedData {

        public virtual T GetValue() {
            throw new NotImplementedException();
        }
        public virtual T GetValue<TArg>(TArg customArg) {
            Debug.Log(GetType());
            throw new NotImplementedException();
        }
    }

}

