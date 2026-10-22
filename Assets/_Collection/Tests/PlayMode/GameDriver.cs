using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AllMightyOne.Tests
{
    /// <summary>
    /// Test assemblies cannot reference Assembly-CSharp, so game components are driven through reflection.
    /// </summary>
    public static class GameDriver
    {
        public static Type GameType(string fullName) =>
            Type.GetType($"{fullName}, Assembly-CSharp")
            ?? AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(fullName)).FirstOrDefault(t => t != null)
            ?? throw new Exception($"Type {fullName} not found");

        public static Component Find(string fullName) =>
            (Component)Object.FindFirstObjectByType(GameType(fullName), FindObjectsInactive.Include);

        public static object Call(object target, string method, params object[] args)
        {
            var t = target.GetType();
            var mi = t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .First(m => m.Name == method && m.GetParameters().Length == args.Length);
            return mi.Invoke(target, args);
        }

        public static T Get<T>(object target, string member)
        {
            var t = target.GetType();
            var p = t.GetProperty(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null) return (T)p.GetValue(target);
            var f = t.GetField(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return (T)f.GetValue(target);
        }

        public static void Set(object target, string member, object value)
        {
            var t = target.GetType();
            var f = t.GetField(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null) { f.SetValue(target, value); return; }
            t.GetProperty(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
        }

        /// <summary> MOST_Database.Get&lt;IntData&gt;(name) through reflection; returns the IntData object. </summary>
        public static object GetIntData(object database, string name)
        {
            var intType = GameType("Solo.MOST_IN_ONE.IntData");
            var get = database.GetType().GetMethods().First(m => m.Name == "Get" && m.IsGenericMethod && m.GetParameters()[0].ParameterType == typeof(string));
            return get.MakeGenericMethod(intType).Invoke(database, new object[] { name });
        }

        public static int GetInt(object database, string name) => Get<int>(GetIntData(database, name), "Value");
        public static void SetInt(object database, string name, int value) => Set(GetIntData(database, name), "Value", value);

        public static IEnumerator LoadScene(string path)
        {
#if UNITY_EDITOR
            var op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
            while (!op.isDone) yield return null;
#else
            yield return SceneManager.LoadSceneAsync(System.IO.Path.GetFileNameWithoutExtension(path));
#endif
            yield return null;
            yield return null;
        }

        public static GameObject FindObject(string name) =>
            Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.scene.IsValid() && g.name == name);

        public static void Click(string buttonName)
        {
            var go = FindObject(buttonName) ?? throw new Exception($"Button {buttonName} not found");
            go.GetComponent<Button>().onClick.Invoke();
        }

        public static IEnumerator WaitUntil(Func<bool> condition, float timeout, string what)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) throw new TimeoutException($"Timed out waiting for {what}");
                yield return null;
            }
        }
    }
}
