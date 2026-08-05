using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Linq;
using UnityEngine;

namespace CryingSnow.FarmingIsland
{
    public static class SaveSystem
    {
        /// <summary>
        /// Saves data to a file with a specified name.
        /// </summary>
        /// <typeparam name="T">The type of the data to be saved.</typeparam>
        /// <param name="data">The data to be saved.</param>
        /// <param name="fileName">The name of the file to save the data to.</param>
        public static void SaveData<T>(T data, string fileName)
        {
            // Construct the full path to the save file
            string filePath = Application.persistentDataPath + "/" + fileName + ".dat";

            // Create a new BinaryFormatter for serialization
            BinaryFormatter formatter = new BinaryFormatter();

            // Open a file stream to create or overwrite the file
            using (FileStream fileStream = new FileStream(filePath, FileMode.Create))
            {
                // Serialize the data to the file
                formatter.Serialize(fileStream, data);
            }
        }

        /// <summary>
        /// Loads data from a file with a specified name.
        /// </summary>
        /// <typeparam name="T">The type of the data to be loaded.</typeparam>
        /// <param name="fileName">The name of the file to load the data from.</param>
        /// <returns>The loaded data, or default(T) if the file does not exist.</returns>
        public static T LoadData<T>(string fileName)
        {
            // Construct the full path to the save file
            string filePath = Application.persistentDataPath + "/" + fileName + ".dat";

            // Check if the file exists before attempting to load it
            if (File.Exists(filePath))
            {
                // Create a new BinaryFormatter for deserialization
                BinaryFormatter formatter = new BinaryFormatter();

                // Open a file stream to read the file
                using (FileStream fileStream = new FileStream(filePath, FileMode.Open))
                {
                    // Deserialize the data from the file
                    T data = (T)formatter.Deserialize(fileStream);

                    return data;
                }
            }
            else
            {
                // Optional: Log an error message if the file does not exist
                // Debug.LogError("Save file not found in " + filePath);

                // Return default value if the file does not exist
                return default(T);
            }
        }

        /// <summary>
        /// Retrieves the name of the most recently modified save file (without the extension).
        /// </summary>
        /// <returns>The name of the latest save file without its extension, or <c>null</c> if no save files are found.</returns>
        public static string GetLatestSaveFileName()
        {
            // Get all .dat save files from the persistent data path
            var saveFiles = Directory.GetFiles(Application.persistentDataPath, "*.dat");

            // Order the files by their last write time in descending order and return the file name without the extension
            return saveFiles.OrderByDescending(File.GetLastWriteTime)
                            .Select(Path.GetFileNameWithoutExtension)
                            .FirstOrDefault(); // Returns null if no save files are found
        }

        /// <summary>
        /// Checks whether a save file with the specified name exists.
        /// </summary>
        /// <param name="fileName">The name of the save file (without extension) to check.</param>
        /// <returns><c>true</c> if a save file with the given name exists; otherwise, <c>false</c>.</returns>
        public static bool SaveFileExists(string fileName)
        {
            // Get all .dat save files from the persistent data path and extract their names without extensions
            var saveFiles = Directory.GetFiles(Application.persistentDataPath, "*.dat")
                                     .Select(Path.GetFileNameWithoutExtension)
                                     .ToList();

            // Check if the list contains the specified file name
            return saveFiles.Contains(fileName);
        }
    }
}
