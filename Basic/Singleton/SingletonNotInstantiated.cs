using System;

namespace com.dpeter99.utils.Basic
{
    class SingletonNotInstantiated: Exception
    {
        public SingletonNotInstantiated(Type type):base($"{type.Name} is not yet Instantiated")
        {

        }
    }
}
