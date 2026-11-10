using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Morsoul.Flow.Editor
{
    /// <summary>
    /// Batchmode entry point for the EditMode suite — `-runTests` is not
    /// honoured by this editor build, so CI/CLI runs go through:
    ///
    ///   Unity.exe -batchmode -projectPath &lt;proj&gt;
    ///       -executeMethod Morsoul.Flow.Editor.BatchTestRunner.RunEditMode
    ///       -testResults &lt;absolute path&gt; -quit
    ///
    /// Writes NUnit-style XML to the -testResults argument (or
    /// Library/EditModeResults.xml when absent) and exits 0/1 on pass/fail.
    /// </summary>
    public static class BatchTestRunner
    {
        public static void RunEditMode()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Callbacks(ResultsPath()));
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
        }

        static string ResultsPath()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], "-testResults", StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return Path.Combine(Directory.GetCurrentDirectory(),
                "Library", "EditModeResults.xml");
        }

        class Callbacks : ICallbacks
        {
            readonly string _path;

            public Callbacks(string path) => _path = path;

            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                try
                {
                    string dir = Path.GetDirectoryName(_path);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    var document = new System.Xml.XmlDocument();
                    document.LoadXml(result.ToXml().OuterXml);
                    using (var writer = System.Xml.XmlWriter.Create(_path,
                               new System.Xml.XmlWriterSettings { Indent = true }))
                        document.Save(writer);
                    Debug.Log($"BatchTestRunner: {result.PassCount} passed, " +
                              $"{result.FailCount} failed -> {_path}");
                    EditorApplication.Exit(result.FailCount == 0 ? 0 : 1);
                }
                catch (Exception e)
                {
                    Debug.LogError($"BatchTestRunner: failed writing results — {e}");
                    EditorApplication.Exit(2);
                }
            }
        }
    }
}
