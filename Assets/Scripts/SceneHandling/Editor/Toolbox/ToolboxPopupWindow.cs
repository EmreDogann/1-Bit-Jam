using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SceneHandling.Editor.Toolbox
{
    internal class ToolboxPopupWindow : PopupWindowContent
    {
        private readonly IReadOnlyList<ITool> _tools;
        private readonly GUIContent _title;
        private readonly GUIStyle _titleStyle;
        private readonly GUIStyle _horizontalLineStyle;

        public ToolboxPopupWindow(IReadOnlyList<ITool> tools, GUIContent popupTitle, GUIStyle popupTitleStyle = null)
        {
            _tools = tools;
            _title = popupTitle;
            _titleStyle = popupTitleStyle;

            _horizontalLineStyle = new GUIStyle(GUI.skin.box)
            {
                normal =
                {
                    background = EditorGUIUtility.whiteTexture
                },
                stretchWidth = true,
                stretchHeight = false,
                fixedHeight = 2,
                margin = new RectOffset(0, 0, 3, 2),
                padding = new RectOffset()
            };
        }

        public override void OnGUI(Rect rect)
        {
            if (!string.IsNullOrEmpty(_title.text))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(_title, _titleStyle ?? EditorStyles.boldLabel);
                GUILayout.EndHorizontal();

                Color originalColor = GUI.color;
                GUI.color = new Color(1, 1, 1, 0.04f);

                GUILayout.Box("", _horizontalLineStyle);

                GUI.color = originalColor;
            }

            if (_tools.Count == 0)
            {
                GUILayout.Space(2);
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("No tools available.", new GUIStyle { normal = { textColor = Color.grey } });
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            foreach (ITool tool in _tools)
            {
                tool.Draw(Close);
            }
        }

        public override Vector2 GetWindowSize()
        {
            GUIStyle style = _titleStyle ?? EditorStyles.boldLabel;
            int width = (int)Mathf.Max(150, style.CalcSize(_title).x + 20.0f);
            const int bottomPadding = 5;
            float contentHeight = _tools.Count == 0
                ? EditorGUIUtility.singleLineHeight
                : _tools.Sum(x => x.GetHeight());

            float titleHeight = string.IsNullOrEmpty(_title.text)
                ? 0.0f
                : EditorGUIUtility.singleLineHeight + _horizontalLineStyle.CalcHeight(GUIContent.none, width);
            return new Vector2(width, titleHeight + contentHeight + bottomPadding);
        }

        private void Close()
        {
            editorWindow.Close();
        }
    }
}