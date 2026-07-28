using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CalmSpace.Editor
{
    /// <summary>
    /// Imports the original menu artwork and deterministically synthesizes the
    /// demo's royalty-free ambient soundtrack and reusable rounded UI sprite.
    /// </summary>
    internal static class CalmSpaceDemoAssetBuilder
    {
        public const string MenuBackgroundPath =
            "Assets/CalmSpace/UI/Art/CalmSpaceMenuBackground.png";
        public const string RoundedSpritePath =
            "Assets/CalmSpace/UI/Art/RoundedRectangle.png";
        public const string AppIconPath =
            "Assets/CalmSpace/UI/Art/CalmSpaceAppIcon.png";
        public const string AmbientLoopPath =
            "Assets/CalmSpace/Audio/QuietAtelierLoop.wav";

        private const int SampleRate = 44100;
        private const int LoopDurationSeconds = 32;
        private const int ChannelCount = 2;
        private const int BitsPerSample = 16;

        public static Sprite ImportMenuBackground()
        {
            string fullPath = Path.GetFullPath(MenuBackgroundPath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(
                    "The generated Calm Space menu background is missing.",
                    fullPath);
            }

            AssetDatabase.ImportAsset(
                MenuBackgroundPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(
                MenuBackgroundPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    "Unity could not load the menu background importer.");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression =
                TextureImporterCompression.CompressedHQ;

            var androidSettings =
                importer.GetPlatformTextureSettings("Android");
            androidSettings.overridden = true;
            androidSettings.maxTextureSize = 2048;
            androidSettings.format = TextureImporterFormat.ASTC_6x6;
            androidSettings.textureCompression =
                TextureImporterCompression.CompressedHQ;
            importer.SetPlatformTextureSettings(androidSettings);
            importer.SaveAndReimport();

            Sprite sprite =
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    MenuBackgroundPath);
            if (sprite == null)
            {
                throw new InvalidOperationException(
                    "The menu background did not import as a Sprite.");
            }

            return sprite;
        }

        public static Texture2D ImportAppIcon()
        {
            string fullPath = Path.GetFullPath(AppIconPath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(
                    "The generated Calm Space app icon is missing.",
                    fullPath);
            }

            AssetDatabase.ImportAsset(
                AppIconPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(
                AppIconPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    "Unity could not load the app icon importer.");
            }

            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 1024;
            importer.textureCompression =
                TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            Texture2D texture =
                AssetDatabase.LoadAssetAtPath<Texture2D>(AppIconPath);
            if (texture == null)
            {
                throw new InvalidOperationException(
                    "The app icon did not import as a Texture2D.");
            }

            return texture;
        }

        public static Sprite CreateOrLoadRoundedSprite()
        {
            string fullPath = Path.GetFullPath(RoundedSpritePath);
            if (!File.Exists(fullPath))
            {
                File.WriteAllBytes(
                    fullPath,
                    BuildRoundedRectanglePng());
            }

            AssetDatabase.ImportAsset(
                RoundedSpritePath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(
                RoundedSpritePath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    "Unity could not load the rounded sprite importer.");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 64f;
            importer.spriteBorder = new Vector4(18f, 18f, 18f, 18f);
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression =
                TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            Sprite sprite =
                AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
            if (sprite == null)
            {
                throw new InvalidOperationException(
                    "The rounded UI texture did not import as a Sprite.");
            }

            return sprite;
        }

        public static AudioClip CreateOrLoadAmbientLoop()
        {
            string fullPath = Path.GetFullPath(AmbientLoopPath);
            if (!File.Exists(fullPath))
            {
                File.WriteAllBytes(fullPath, BuildAmbientWave());
            }

            AssetDatabase.ImportAsset(
                AmbientLoopPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            var importer =
                AssetImporter.GetAtPath(AmbientLoopPath) as AudioImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    "Unity could not load the ambient loop importer.");
            }

            importer.forceToMono = false;
            importer.loadInBackground = true;
            importer.defaultSampleSettings =
                new AudioImporterSampleSettings
                {
                    loadType = AudioClipLoadType.CompressedInMemory,
                    compressionFormat =
                        AudioCompressionFormat.Vorbis,
                    quality = 0.52f,
                    preloadAudioData = true,
                    sampleRateSetting =
                        AudioSampleRateSetting.PreserveSampleRate
                };
            importer.SaveAndReimport();

            AudioClip clip =
                AssetDatabase.LoadAssetAtPath<AudioClip>(
                    AmbientLoopPath);
            if (clip == null)
            {
                throw new InvalidOperationException(
                    "The original ambient loop did not import.");
            }

            return clip;
        }

        public static void ConfigureSnapImport(string snapClipPath)
        {
            var importer =
                AssetImporter.GetAtPath(snapClipPath) as AudioImporter;
            if (importer == null)
            {
                return;
            }

            importer.forceToMono = true;
            importer.loadInBackground = false;
            importer.defaultSampleSettings =
                new AudioImporterSampleSettings
                {
                    loadType = AudioClipLoadType.DecompressOnLoad,
                    compressionFormat =
                        AudioCompressionFormat.PCM,
                    quality = 1f,
                    preloadAudioData = true,
                    sampleRateSetting =
                        AudioSampleRateSetting.PreserveSampleRate
                };
            importer.SaveAndReimport();
        }

        private static byte[] BuildRoundedRectanglePng()
        {
            const int size = 64;
            const float radius = 17f;
            var texture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false,
                true);
            var pixels = new Color32[size * size];

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    float centeredX =
                        Mathf.Abs(x + 0.5f - size * 0.5f) -
                        (size * 0.5f - radius);
                    float centeredY =
                        Mathf.Abs(y + 0.5f - size * 0.5f) -
                        (size * 0.5f - radius);
                    float outsideX = Mathf.Max(centeredX, 0f);
                    float outsideY = Mathf.Max(centeredY, 0f);
                    float signedDistance =
                        Mathf.Sqrt(
                            outsideX * outsideX +
                            outsideY * outsideY) -
                        radius;
                    byte alpha = (byte)Mathf.RoundToInt(
                        Mathf.Clamp01(0.5f - signedDistance) * 255f);
                    pixels[y * size + x] =
                        new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            byte[] png = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);
            return png;
        }

        private static byte[] BuildAmbientWave()
        {
            int frameCount = SampleRate * LoopDurationSeconds;
            int dataLength =
                frameCount * ChannelCount * (BitsPerSample / 8);

            using (var stream = new MemoryStream(44 + dataLength))
            using (var writer = new BinaryWriter(stream))
            {
                WriteWaveHeader(writer, dataLength);

                for (var frame = 0; frame < frameCount; frame++)
                {
                    double time = frame / (double)SampleRate;
                    float left = SynthesizeChannel(time, -1f);
                    float right = SynthesizeChannel(time, 1f);
                    writer.Write(FloatToPcm16(left));
                    writer.Write(FloatToPcm16(right));
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        private static float SynthesizeChannel(
            double time,
            float stereoSide)
        {
            int chordIndex = Math.Min(
                3,
                (int)(time / 8d));
            double chordTime = time - chordIndex * 8d;
            double padEnvelope = Math.Pow(
                Math.Sin(Math.PI * chordTime / 8d),
                0.42d);
            double slowBreath =
                0.88d +
                0.12d * Math.Sin(
                    Math.PI * 2d *
                    QuantizePeriodic(0.0625d) *
                    time +
                    stereoSide * 0.36d);

            double[] chord = GetChord(chordIndex);
            double pad = 0d;
            for (var note = 0; note < chord.Length; note++)
            {
                double detune = stereoSide *
                    (note % 2 == 0 ? 0.0018d : -0.0014d);
                double frequency = QuantizePeriodic(
                    chord[note] * (1d + detune));
                double phase =
                    note * 0.83d + stereoSide * (0.12d + note * 0.04d);
                pad += Math.Sin(
                    Math.PI * 2d * frequency * time + phase);
            }

            pad =
                pad / chord.Length *
                padEnvelope *
                slowBreath *
                0.34d;

            double porcelain =
                SynthesizeChime(time, 2.50d, 587.33d, stereoSide, -0.3d) +
                SynthesizeChime(time, 6.25d, 440.00d, stereoSide, 0.2d) +
                SynthesizeChime(time, 10.50d, 659.25d, stereoSide, 0.35d) +
                SynthesizeChime(time, 14.75d, 493.88d, stereoSide, -0.2d) +
                SynthesizeChime(time, 18.25d, 440.00d, stereoSide, -0.4d) +
                SynthesizeChime(time, 22.50d, 587.33d, stereoSide, 0.25d) +
                SynthesizeChime(time, 26.25d, 659.25d, stereoSide, 0.4d);

            double air =
                Math.Sin(
                    Math.PI * 2d *
                    QuantizePeriodic(173.0d + stereoSide * 1.2d) *
                    time +
                    1.7d) *
                0.008d;
            air +=
                Math.Sin(
                    Math.PI * 2d *
                    QuantizePeriodic(239.0d - stereoSide * 0.9d) *
                    time +
                    0.4d) *
                0.006d;
            air *= Math.Sin(Math.PI * time / LoopDurationSeconds);

            double signal = pad + porcelain * 0.21d + air;
            return (float)Math.Max(-0.82d, Math.Min(0.82d, signal));
        }

        private static double SynthesizeChime(
            double time,
            double start,
            double frequency,
            float stereoSide,
            double pan)
        {
            double local = time - start;
            if (local < 0d || local > 3.8d)
            {
                return 0d;
            }

            double envelope =
                (1d - Math.Exp(-local * 16d)) *
                Math.Exp(-local * 1.65d);
            double sideGain =
                Math.Max(0.55d, 1d + stereoSide * pan * 0.38d);
            double fundamental = QuantizePeriodic(
                frequency * (1d + stereoSide * 0.0008d));
            double shimmer = QuantizePeriodic(frequency * 2.006d);
            return envelope * sideGain *
                (Math.Sin(Math.PI * 2d * fundamental * local) *
                 0.74d +
                 Math.Sin(
                     Math.PI * 2d * shimmer * local + 0.35d) *
                 0.26d);
        }

        private static double[] GetChord(int chordIndex)
        {
            switch (chordIndex)
            {
                case 0:
                    return new[]
                    {
                        146.83d,
                        184.997d,
                        220.000d,
                        277.183d,
                        329.628d
                    };
                case 1:
                    return new[]
                    {
                        138.591d,
                        164.814d,
                        220.000d,
                        246.942d,
                        329.628d
                    };
                case 2:
                    return new[]
                    {
                        123.471d,
                        146.832d,
                        184.997d,
                        220.000d,
                        277.183d
                    };
                default:
                    return new[]
                    {
                        98.000d,
                        123.471d,
                        146.832d,
                        184.997d,
                        246.942d
                    };
            }
        }

        private static double QuantizePeriodic(double frequency)
        {
            return Math.Round(
                frequency * LoopDurationSeconds) /
                LoopDurationSeconds;
        }

        private static short FloatToPcm16(float sample)
        {
            return (short)Mathf.RoundToInt(
                Mathf.Clamp(sample, -1f, 1f) *
                short.MaxValue);
        }

        private static void WriteWaveHeader(
            BinaryWriter writer,
            int dataLength)
        {
            const short audioFormat = 1;
            const short channelCount = ChannelCount;
            const short bitsPerSample = BitsPerSample;
            const int bytesPerSample = BitsPerSample / 8;

            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataLength);
            writer.Write(new[] { 'W', 'A', 'V', 'E' });
            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write(audioFormat);
            writer.Write(channelCount);
            writer.Write(SampleRate);
            writer.Write(
                SampleRate * channelCount * bytesPerSample);
            writer.Write((short)(channelCount * bytesPerSample));
            writer.Write(bitsPerSample);
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataLength);
        }
    }
}
