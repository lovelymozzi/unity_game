using UnityEditor;
using UnityEngine;
using System.IO;

namespace CryingSnow.FarmingIsland
{
    public class SaveFileManager : EditorWindow
    {
        private string saveFilesDirectory;
        private FileInfo[] saveFiles;

        [MenuItem("Tools/Farming Island/Save File Manager")]
        public static void ShowWindow()
        {
            GetWindow<SaveFileManager>("Save File Manager");
        }

        private void OnEnable()
        {
            saveFilesDirectory = Application.persistentDataPath;
            RefreshSaveFiles();
        }

        private void RefreshSaveFiles()
        {
            if (Directory.Exists(saveFilesDirectory))
            {
                saveFiles = new DirectoryInfo(saveFilesDirectory).GetFiles("*.dat");
            }
            else
            {
                saveFiles = new FileInfo[0];
            }
        }

        private void OnGUI()
        {
            GUILayout.Label("Save File Manager", EditorStyles.boldLabel);
            GUILayout.Label($"Save files directory: {saveFilesDirectory}", EditorStyles.wordWrappedLabel);

            if (saveFiles == null || saveFiles.Length == 0)
            {
                GUILayout.Label("No save files found.", EditorStyles.helpBox);
            }
            else
            {
                foreach (var file in saveFiles)
                {
                    GUILayout.BeginHorizontal();

                    GUILayout.Label(file.Name, GUILayout.ExpandWidth(true));

                    if (GUILayout.Button("Delete", GUILayout.Width(100)))
                    {
                        if (EditorUtility.DisplayDialog(
                            "Confirm Delete",
                            $"Are you sure you want to delete the save file '{file.Name}'?",
                            "Yes",
                            "No"))
                        {
                            File.Delete(file.FullName);
                            RefreshSaveFiles();
                            Repaint();
                        }
                    }

                    GUILayout.EndHorizontal();
                }

                GUILayout.Space(10);

                if (GUILayout.Button("Delete All Save Files"))
                {
                    if (EditorUtility.DisplayDialog(
                        "Confirm Delete All",
                        "Are you sure you want to delete all save files? This action cannot be undone.",
                        "Yes",
                        "No"))
                    {
                        foreach (var file in saveFiles)
                        {
                            File.Delete(file.FullName);
                        }

                        RefreshSaveFiles();
                        Repaint();
                    }
                }
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Refresh", GUILayout.Height(30)))
            {
                RefreshSaveFiles();
            }
        }
    }
}
