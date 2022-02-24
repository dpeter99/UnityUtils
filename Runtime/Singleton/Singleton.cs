using JetBrains.Annotations;
using UnityEngine;

namespace com.dpeter99.utils.Basic
{
    public class Singleton<T> where T : Singleton<T>, new()
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
                if (_instance != null)
                    throw new SingletonMultipleInstanceException(typeof(T));
                _instance = value;
            }
        }

        public Singleton()
        {
            Instance = (T) this;
        }
    }
}