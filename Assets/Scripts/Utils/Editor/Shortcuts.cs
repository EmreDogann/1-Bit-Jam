using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Editor
{
    public class Shortcuts
    {
        // Shift + R
        [Shortcut("Force Domain Reload", KeyCode.R, ShortcutModifiers.Shift)]
        public static void DomainReload()
        {
            EditorUtility.RequestScriptReload();
        }

        // Alt + C
        [Shortcut("Clear Console", KeyCode.C, ShortcutModifiers.Alt)]
        public static void ClearConsole()
        {
            Assembly assembly = Assembly.GetAssembly(typeof(SceneView));
            Type type = assembly.GetType("UnityEditor.LogEntries");
            MethodInfo method = type.GetMethod("Clear");
            method.Invoke(new object(), null);
        }
    }
}