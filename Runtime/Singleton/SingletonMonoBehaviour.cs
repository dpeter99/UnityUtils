using JetBrains.Annotations;
using UnityEngine;

namespace com.dpeter99.utils.Basic
{

    public class SingletonMonoBehaviour<T>: MonoBehaviour where T : SingletonMonoBehaviour<T>, new()
    {
        [CanBeNull] static T _instance;

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    throw new SingletonNotInstantiated(typeof(T));
                    //_instance = new T();
                }

                return _instance;
            }

            internal set
            {
            #if UNITY_EDITOR
                if (_instance != null)
                    throw new SingletonMultipleInstanceException(typeof(T));
            #endif
                _instance = value;
            }
        }

        public SingletonMonoBehaviour()
        {
            Instance = (T) this;
        }
    }
}