using UnityEngine;

namespace DeckBattle
{
    public static class MobileFrameRateBootstrap
    {
        private const int MobileTargetFrameRate = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
#if UNITY_ANDROID || UNITY_IOS
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = MobileTargetFrameRate;
#endif
        }
    }
}
