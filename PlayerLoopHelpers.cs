using System;
using UnityEngine.LowLevel;

namespace com.dpeter99.utils
{
    public class PlayerLoopHelpers
    { 
        static bool AppendSystemToPlayerLoopListImpl(PlayerLoopSystem system, ref UnityEngine.LowLevel.PlayerLoopSystem playerLoop, Type playerLoopSystemType)
        {
            if (playerLoop.type == playerLoopSystemType)
            {
                //var del = new DummyDelegateWrapper(system);
                int oldListLength = (playerLoop.subSystemList != null) ? playerLoop.subSystemList.Length : 0;
                var newSubsystemList = new UnityEngine.LowLevel.PlayerLoopSystem[oldListLength + 1];
                for (var i = 0; i < oldListLength; ++i)
                    newSubsystemList[i] = playerLoop.subSystemList[i];
                newSubsystemList[oldListLength] = system;
                
                playerLoop.subSystemList = newSubsystemList;
                return true;
            }
            if (playerLoop.subSystemList != null)
            {
                for(int i=0; i<playerLoop.subSystemList.Length; ++i)
                {
                    if (AppendSystemToPlayerLoopListImpl(system, ref playerLoop.subSystemList[i], playerLoopSystemType))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Add an ECS system to a specific point in the Unity player loop, so that it is updated every frame.
        /// </summary>
        /// <remarks>
        /// This function does not change the currently active player loop. If this behavior is desired, it's necessary
        /// to call PlayerLoop.SetPlayerLoop(playerLoop) after the systems have been removed.
        /// </remarks>
        /// <param name="system">The system to add to the player loop.</param>
        /// <param name="playerLoop">Existing player loop to modify (e.g. PlayerLoop.GetCurrentPlayerLoop())</param>
        /// <param name="playerLoopSystemType">The Type of the PlayerLoopSystem subsystem to which the ECS system should be appended.
        /// See the UnityEngine.PlayerLoop namespace for valid values.</param>
        public static void AppendSystemToPlayerLoopList(PlayerLoopSystem system, ref UnityEngine.LowLevel.PlayerLoopSystem playerLoop, Type playerLoopSystemType)
        {
            if (!AppendSystemToPlayerLoopListImpl(system, ref playerLoop, playerLoopSystemType))
            {
                throw new ArgumentException(
                    $"Could not find PlayerLoopSystem with type={playerLoopSystemType}");
            }
        }
    }
}