using System;
using UnityEngine;
using Verse;

namespace XpCustomizer.UI
{
    /// <summary>
    /// L3-F3: XP Customizer-owned reference text only. No global GUI styles,
    /// gameplay settings, vanilla patches, or persistent state are changed.
    /// </summary>
    internal static class VanillaReferenceAccentUI
    {
        private static readonly Color Muted = new Color(0.76f, 0.76f, 0.76f, 1f);
        private static readonly Color VanillaGold = new Color(0.96f, 0.82f, 0.48f, 1f);
        private const float SegmentGap = 12f;
        private const float VerticalGap = 2f;

#if DEBUG
        internal const string Version = "XPC-L10N-L3F3-GOLD-20261112-R1";
        private static bool versionLogged;
        private static bool progressionVersionLogged;
        private static bool missingKeyLogged;
#endif

        /// <summary>
        /// Render two explicitly translated segments. A narrow row wraps at the
        /// segment boundary; a long segment wraps within its own measured row.
        /// The legacy key remains available as a safe, non-gold fallback.
        /// </summary>
        internal static void Draw(Listing_Standard listing, string mutedKey,
            string vanillaKey, bool vanillaFirst, string legacyKey)
        {
            DrawSegments(listing, mutedKey, vanillaKey, vanillaFirst, legacyKey,
                true);
        }

        /// <summary>
        /// Exactly the same two-segment renderer as Learning, with a separate
        /// one-shot progression build probe. Preserves the Learning log meaning.
        /// </summary>
        internal static void DrawProgressionRange(Listing_Standard listing,
            string mutedKey, string vanillaKey, string legacyKey)
        {
            if (listing == null) return;
#if DEBUG
            LogProgressionReferenceOnce();
#endif
            DrawSegments(listing, mutedKey, vanillaKey, false, legacyKey,
                false);
        }

        /// <summary>
        /// Keep Listing_Standard's native label height, font and wrapping.
        /// Color only the XP Customizer-owned fixed vanilla comparison value.
        /// </summary>
        internal static void DrawVanillaLabel(Listing_Standard listing,
            string label)
        {
            if (listing == null) return;
            Color previousColor = GUI.color;
            try
            {
                GUI.color = new Color(VanillaGold.r, VanillaGold.g,
                    VanillaGold.b, previousColor.a);
                listing.Label(label);
            }
            finally
            {
                GUI.color = previousColor;
            }
        }

#if DEBUG
        private static void LogProgressionReferenceOnce()
        {
            if (progressionVersionLogged || Event.current == null ||
                Event.current.type != EventType.Repaint) return;
            progressionVersionLogged = true;
            Log.Message("[XPC-L10N-L3F3][PROGRESSION_VANILLA_REFERENCE] " +
                "version=XPC-L3F3-VANILLA-REF-EXPANSION-20261112-R1; " +
                "scope=DISPLAY_ONLY; sections=CAREER,RESERVE,TRAINER; rows=6; " +
                "stageB=RETAINED; build=" + XpCustomizerMod.BuildId);
        }
#endif

        private static void DrawSegments(Listing_Standard listing,
            string mutedKey, string vanillaKey, bool vanillaFirst,
            string legacyKey, bool isLearningReference)
        {
            if (listing == null) return;

            string muted = TranslateOrNull(mutedKey);
            string vanilla = TranslateOrNull(vanillaKey);
            if (muted == null || vanilla == null)
            {
#if DEBUG
                if (!missingKeyLogged)
                {
                    missingKeyLogged = true;
                    Log.Warning("[XPC-L10N-L3F3][MISSING_KEY] " +
                        "fallback=" + legacyKey + "; muted=" + mutedKey +
                        "; vanilla=" + vanillaKey + "; build=" + XpCustomizerMod.BuildId);
                }
#endif
                // On an incomplete language pack, preserve the original full
                // explanation instead of rendering untranslated keys.
                SettingsUIHelpers.DrawReferenceText(listing,
                    legacyKey.Translate().ToString());
                return;
            }

#if DEBUG
            if (isLearningReference && !versionLogged)
            {
                versionLogged = true;
                Log.Message("[XPC-L10N-L3F3][LEARNING_REFERENCE] " +
                    "version=" + Version + "; screen=LEARNING; " +
                    "sampleKey=" + vanillaKey + "; sample=" + vanilla +
                    "; vanillaGold=0.96,0.82,0.48,1; scope=DISPLAY_ONLY; build=" +
                    XpCustomizerMod.BuildId);
            }
#endif
            Color previousColor = GUI.color;
            GameFont previousFont = Text.Font;
            TextAnchor previousAnchor = Text.Anchor;
            bool previousWordWrap = Text.WordWrap;
            try
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;

                string first = vanillaFirst ? vanilla : muted;
                string second = vanillaFirst ? muted : vanilla;
                Color firstColor = vanillaFirst ? VanillaGold : Muted;
                Color secondColor = vanillaFirst ? Muted : VanillaGold;

                // Use the active RimWorld font, not hard-coded glyph widths.
                float width = Mathf.Max(1f, listing.ColumnWidth);
                float firstWidth = Mathf.Ceil(Text.CalcSize(first).x) + 3f;
                float secondWidth = Mathf.Ceil(Text.CalcSize(second).x) + 3f;
                if (firstWidth + SegmentGap + secondWidth <= width)
                {
                    float height = Mathf.Max(RowHeight(first, firstWidth),
                        RowHeight(second, secondWidth));
                    Rect row = listing.GetRect(height);
                    DrawPart(new Rect(row.x, row.y, firstWidth, height), first,
                        firstColor, previousColor.a);
                    DrawPart(new Rect(row.x + firstWidth + SegmentGap, row.y,
                        secondWidth, height), second, secondColor, previousColor.a);
                }
                else
                {
                    float firstHeight = RowHeight(first, width);
                    Rect firstRow = listing.GetRect(firstHeight);
                    DrawPart(firstRow, first, firstColor, previousColor.a);

                    listing.Gap(VerticalGap);
                    float secondHeight = RowHeight(second, width);
                    Rect secondRow = listing.GetRect(secondHeight);
                    DrawPart(secondRow, second, secondColor, previousColor.a);
                }
            }
            finally
            {
                Text.Font = previousFont;
                Text.Anchor = previousAnchor;
                Text.WordWrap = previousWordWrap;
                GUI.color = previousColor;
            }
        }

        private static string TranslateOrNull(string key)
        {
            if (String.IsNullOrEmpty(key)) return null;
            string value = key.Translate().ToString();
            return String.IsNullOrEmpty(value) ||
                String.Equals(value, key, StringComparison.Ordinal) ? null : value;
        }

        private static float RowHeight(string text, float width)
        {
            return Mathf.Max(24f,
                Mathf.Ceil(Text.CalcHeight(text, Mathf.Max(1f, width))) + 4f);
        }

        private static void DrawPart(Rect rect, string text, Color tint, float oldAlpha)
        {
            GUI.color = new Color(tint.r, tint.g, tint.b, oldAlpha);
            Widgets.Label(rect, text);
        }
    }
}
