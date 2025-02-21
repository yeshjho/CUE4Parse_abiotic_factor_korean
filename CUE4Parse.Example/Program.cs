using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Sound;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Versions;
using CUE4Parse.Utils;
using CUE4Parse_Conversion.Textures;
using Newtonsoft.Json;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using SharpGLTF.Schema2;
using Buffer = System.Buffer;

namespace CUE4Parse.Example
{
    public static class Program
    {
        const string VERSION = "0.10.0.15580";

        private const string _gameDirectory = "C:\\Users\\yeshj\\Desktop\\folders\\Pycharm\\abiotic_korean\\archive\\pack\\vanilla\\" + VERSION;
        //private const string _gameDirectory = "C:\\Users\\yeshj\\Desktop\\temp"; // Change game directory path to the one you have.
        //private const string _aesKey = "0xF271F4B1EA375C42D3676058BAE8FBA295CB61F773070A706A48EAD7C6F98CDB";

        private const string _mapping = "C:\\Users\\yeshj\\Desktop\\folders\\Pycharm\\abiotic_korean\\archive\\usmap\\" + VERSION + ".usmap";
        //private const string _objectPath = "AbioticFactor/Content/";
        //private const string _objectName = "FortCosmeticCharacterPartVariant_0";

        private const string _outputDirectory = "C:\\Users\\yeshj\\Desktop\\folders\\Pycharm\\abiotic_korean\\archive\\offset_annotated\\";

        private static DefaultFileProvider provider;

        // Rick has 2 exports as of today
        //      - CID_A_112_Athena_Commando_M_Ruckus
        //      - FortCosmeticCharacterPartVariant_0
        //
        // this example will show you how to get them all or just one of them

        public static void Main()
        {
            Log.Logger = new LoggerConfiguration().WriteTo.Console(theme: AnsiConsoleTheme.Literate).CreateLogger();

            //var provider = new ApkFileProvider(@"C:\Users\valen\Downloads\ZqOY4K41h0N_Qb6WjEe23TlGExojpQ.apk", true, new VersionContainer(EGame.GAME_UE5_3));
            provider = new DefaultFileProvider(_gameDirectory, SearchOption.TopDirectoryOnly, true, new VersionContainer(EGame.GAME_UE5_3));
            provider.MappingsContainer = new FileUsmapTypeMappingsProvider(_mapping);

            provider.Initialize(); // will scan local files and read them to know what it has to deal with (PAK/UTOC/UCAS/UASSET/UMAP)
            //provider.SubmitKey(new FGuid(), new FAesKey(_aesKey)); // decrypt basic info (1 guid - 1 key)

            provider.LoadLocalization(ELanguage.English); // explicit enough

            provider.Mount();
            //SaveAllToJson();
            SaveAllTextures();
        }

        public static void SaveAllTextures()
        {
            string FixAndCreatePath(DirectoryInfo baseDirectory, string fullPath, string? ext = null)
            {
                if (fullPath.StartsWith('/')) fullPath = fullPath[1..];
                var ret = Path.Combine(baseDirectory.FullName, fullPath) + (ext != null ? $".{ext.ToLower()}" : "");
                Directory.CreateDirectory(ret.Replace('\\', '/').SubstringBeforeLast('/'));
                return ret;
            }

            DirectoryInfo baseDirectory = new DirectoryInfo("C:\\Users\\yeshj\\Desktop\\folders\\Pycharm\\abiotic_korean\\tools\\UE4-DDS-Tools-v0.6.1-Batch\\exported");

            foreach (var file in provider.Files)
            {
                if (!file.Value.IsUE4Package || file.Key.StartsWith("engine"))
                {
                    continue;
                }

                foreach (UObject uObject in provider.LoadAllObjects(file.Key))
                {
                    if (uObject is not UTexture2D t || t.Decode() is not { } bitmap) continue;

                    var texturePath = FixAndCreatePath(baseDirectory, file.Value.PathWithoutExtension.Replace('/', '+'), "png");
                    using var fs = new FileStream(texturePath, FileMode.Create, FileAccess.Write);
                    using var data = bitmap.Encode(ETextureFormat.Png, 100);
                    using var stream = data.AsStream();
                    stream.CopyTo(fs);
                }
            }
        }

        public static void SaveAllDialogueAudioFiles()
        {
            const string fileName = "C:\\Users\\yeshj\\Desktop\\folders\\Pycharm\\abiotic_korean\\out\\dialogue_extra.csv";
            Regex CSVParser = new Regex(",(?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))");

            using (StreamReader reader = new StreamReader(fileName))
            {
                while (reader.ReadLine() is { } line)
                {
                    string[] data = CSVParser.Split(line);
                    var soundWave = provider.LoadObject(data[1].Split('.')[0]) as USoundWave;
                    Decode(soundWave, true, out _, out var audioData);

                    const string soundOutDir = "C:\\Users\\yeshj\\Desktop\\folders\\Pycharm\\abiotic_korean\\out\\sound\\";
                    Directory.CreateDirectory(soundOutDir);

                    string audioFilePath = Path.Join(soundOutDir, data[0] + ".binka");
                    string wavAudioFilePath = Path.Join(soundOutDir, data[0] + ".wav");
                    File.WriteAllBytes(audioFilePath, audioData!);

                    var binkadecProcess = Process.Start(new ProcessStartInfo
                    {
                        FileName = "C:\\Users\\yeshj\\Desktop\\FModel\\Output\\.data\\binkadec.exe",
                        Arguments = $"-i \"{audioFilePath}\" -o \"{wavAudioFilePath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                }
            }
        }

        public static void SaveAllToJson()
        {
            // these 2 lines will load all exports the asset has and transform them in a single Json string
            //var allExports = provider.LoadAllObjects(null);
            //var fullJson = JsonConvert.SerializeObject(allExports, Formatting.Indented);

            //// each exports have a name, these 2 lines will load only one export the asset has
            //// you must use "LoadObject" and provide the full path followed by a dot followed by the export name
            //var variantExport = provider.LoadObject(_objectPath + "." + _objectName);
            //var variantJson = JsonConvert.SerializeObject(variantExport, Formatting.Indented);

            //Console.WriteLine(variantJson); // Outputs the variantJson.
            
            foreach (var file in provider.Files)
            {
                if (!file.Value.IsUE4Package || !file.Key.StartsWith("abioticfactor/content/map"))
                {
                    continue;
                }

                IEnumerable<UObject> uObjects = provider.LoadAllObjects(file.Key);
                string json = JsonConvert.SerializeObject(uObjects, Formatting.Indented);

                string outputPath = Path.Join(_outputDirectory, file.Value.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                using StreamWriter writer = new StreamWriter(Path.ChangeExtension(outputPath, ".json"));
                writer.Write(json);
            }
        }

        public static void Decode(this USoundWave soundWave, bool shouldDecompress, out string audioFormat, out byte[]? data)
        {
            audioFormat = string.Empty;
            byte[]? input = null;

            if (!soundWave.bStreaming)
            {
                if (soundWave.CompressedFormatData != null)
                {
                    var compressedData = soundWave.CompressedFormatData.Formats.First();
                    audioFormat = compressedData.Key.Text.SubstringBefore('_');
                    input = compressedData.Value.Data;
                }

                if (soundWave.RawData?.Data != null) // is this even a thing?
                {
                    audioFormat = string.Empty;
                    input = soundWave.RawData.Data;
                }
            }
            else if (soundWave.RunningPlatformData?.Chunks != null)
            {
                var offset = 0;
                var ret = new byte[soundWave.RunningPlatformData.Chunks.Sum(x => x.AudioDataSize)];
                for (var i = 0; i < soundWave.RunningPlatformData.NumChunks; i++)
                {
                    Buffer.BlockCopy(soundWave.RunningPlatformData.Chunks[i].BulkData.Data, 0, ret, offset, soundWave.RunningPlatformData.Chunks[i].AudioDataSize);
                    offset += soundWave.RunningPlatformData.Chunks[i].AudioDataSize;
                }

                audioFormat = soundWave.RunningPlatformData.AudioFormat.Text;
                input = ret;
            }

            data = input;
        }
    }
}
