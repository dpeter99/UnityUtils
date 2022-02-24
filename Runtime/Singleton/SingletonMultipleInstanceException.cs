using System;

namespace com.dpeter99.utils.Basic
{
    class SingletonMultipleInstanceException: Exception
    {
        public SingletonMultipleInstanceException(Type type) 
            : base($"Singleton type: {type.Name} was instanciated more than once")
        {

        }
    }
}
