using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gustom.Editor
{
    /// Shared IMGUI helpers reused across Gustom editor tools.
    internal static class GustomEditorGUI
    {
        public const float CompactListRowHeight = 22f;
        public const float LargeListRowHeight = 38f;
        public const float CompactListIconSize = 16f;
        public const float LargeListIconSize = 24f;

        private static GUIStyle _rightMiniLabelStyle;
        private static GUIStyle _centeredMiniLabelStyle;
        private static GUIStyle _settingsBoxStyle;
        private static GUIStyle _rowLabelStyle;
        private static GUIStyle _activeRowLabelStyle;

        public static GUIStyle RightMiniLabelStyle => _rightMiniLabelStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleRight,
            clipping = TextClipping.Clip
        };

        public static GUIStyle CenteredMiniLabelStyle => _centeredMiniLabelStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter
        };

        public static GUIStyle SettingsBoxStyle => _settingsBoxStyle ??= new GUIStyle(EditorStyles.helpBox)
        {
            padding = new RectOffset(8, 8, 8, 8)
        };

        public static GUIStyle RowLabelStyle => _rowLabelStyle ??= new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(4, 4, 2, 2),
            margin = new RectOffset()
        };

        public static GUIStyle ActiveRowLabelStyle => _activeRowLabelStyle ??= new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(4, 4, 2, 2),
            margin = new RectOffset()
        };

        public static void DrawTexture(Rect rect, Texture texture, ScaleMode scaleMode = ScaleMode.ScaleToFit, bool alphaBlend = true)
        {
            if (texture && Event.current.type == EventType.Repaint)
                GUI.DrawTexture(rect, texture, scaleMode, alphaBlend);
        }

        /// Draws a ping ("search") icon at pingRect when hover is true, and pings target on click.
        /// Caller is responsible for computing pingRect and the hover condition.
        public static void DrawPingButton(Rect pingRect, Object target, bool hover)
        {
            if (!hover || !target) return;

            Texture icon = EditorGUIUtility.IconContent("Search Icon").image;
            DrawTexture(pingRect, icon);

            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && pingRect.Contains(currentEvent.mousePosition))
            {
                EditorGUIUtility.PingObject(target);
                currentEvent.Use();
            }
        }

        /// Draws alternating-row background plus hover/selected highlight for a list row.
        /// Call during EventType.Repaint only.
        public static void DrawRowBackground(Rect rowRect, int rowIndex, bool isSelected, bool isHovered, bool showStripedRows, Color altRowColor, Color hoverColor, Color? selectedColor = null)
        {
            if (showStripedRows && rowIndex % 2 != 0) EditorGUI.DrawRect(rowRect, altRowColor);
            if (isHovered && !isSelected) EditorGUI.DrawRect(rowRect, hoverColor);
            if (isSelected) EditorGUI.DrawRect(rowRect, selectedColor ?? GUI.skin.settings.selectionColor);
        }

        public static void DrawFooterGuide(string text)
        {
            Rect separatorRect = GUILayoutUtility.GetRect(0f, 1f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(separatorRect, EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.12f) : new Color(0f, 0f, 0f, 0.12f));
            GUILayout.Label(text, EditorStyles.centeredGreyMiniLabel, GUILayout.Height(16f));
        }
    }
}
