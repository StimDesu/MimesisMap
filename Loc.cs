using UnityEngine;

namespace MimesisMap
{
    public enum Lang { Auto, Russian, English }

    /// <summary>Two-language UI text. Auto follows the system language (Russian/Ukrainian/Belarusian -> Russian).</summary>
    internal static class Loc
    {
        public static bool Ru
        {
            get
            {
                var l = Cfg.Language != null ? Cfg.Language.Value : Lang.Auto;
                if (l == Lang.Russian) return true;
                if (l == Lang.English) return false;
                var sys = Application.systemLanguage;
                return sys == SystemLanguage.Russian || sys == SystemLanguage.Ukrainian || sys == SystemLanguage.Belarusian;
            }
        }

        public static string T(string ru, string en) => Ru ? ru : en;
    }
}
