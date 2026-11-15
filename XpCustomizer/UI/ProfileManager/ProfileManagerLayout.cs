using UnityEngine;

namespace XpCustomizer.UI
{
    /// <summary>
    /// Deterministic coordinates for an XP-Customizer-owned dialog, not
    /// Dialog_ModSettings or any other mod's GUI. No I/O or UI side effects.
    /// </summary>
    internal struct ProfileManagerGeometry
    {
        internal Rect Header;
        internal Rect Toolbar;
        internal Rect List;
        internal Rect Detail;
        internal Rect Footer;
        internal bool Stacked;
    }

    internal static class ProfileManagerLayout
    {
        internal const string Revision = "R6P1C15-DETACHED-WINDOW-20261106";
        internal const float StackedThreshold = 690f;
        // Profile manager Stage A: Small-font body geometry, owned by this UI only.
        internal const float HeaderHeight = 54f;
        internal const float FooterHeight = 42f;
        internal const float ListHeaderOffset = 58f;
        internal const float ListRowHeight = 58f;
        internal const float ListRowGap = 4f;

        internal static ProfileManagerGeometry Calculate(Rect area)
        {
            ProfileManagerGeometry g = new ProfileManagerGeometry();
            float width = Mathf.Max(0f, area.width);
            float height = Mathf.Max(0f, area.height);
            g.Header = new Rect(area.x, area.y, width, Mathf.Min(height, HeaderHeight));
            g.Toolbar = new Rect(area.x, g.Header.yMax + 5f,
                width, Mathf.Min(34f, Mathf.Max(0f, area.yMax - g.Header.yMax - 5f)));
            g.Footer = new Rect(area.x, Mathf.Max(g.Toolbar.yMax + 6f, area.yMax - FooterHeight - 1f),
                width, Mathf.Min(FooterHeight, Mathf.Max(0f, area.yMax - g.Toolbar.yMax - 6f)));
            float bodyTop = g.Toolbar.yMax + 10f;
            float bodyHeight = Mathf.Max(0f, g.Footer.y - bodyTop - 8f);
            const float gap = 10f;
            g.Stacked = width < StackedThreshold;
            if (g.Stacked)
            {
                // Keep a usable list viewport on compact stacked layouts while
                // reserving a detail panel. Preserve the 690px breakpoint.
                float first = Mathf.Min(Mathf.Max(0f, bodyHeight - gap),
                    Mathf.Max(0f, Mathf.Min(Mathf.Max(94f, (bodyHeight - gap) * 0.42f),
                        bodyHeight - gap - 80f)));
                g.List = new Rect(area.x, bodyTop, width, first);
                g.Detail = new Rect(area.x, g.List.yMax + gap, width,
                    Mathf.Max(0f, bodyHeight - gap - first));
            }
            else
            {
                float left = Mathf.Floor((width - gap) * 0.38f);
                g.List = new Rect(area.x, bodyTop, left, bodyHeight);
                g.Detail = new Rect(g.List.xMax + gap, bodyTop,
                    Mathf.Max(0f, width - left - gap), bodyHeight);
            }
            return g;
        }
    }
}
