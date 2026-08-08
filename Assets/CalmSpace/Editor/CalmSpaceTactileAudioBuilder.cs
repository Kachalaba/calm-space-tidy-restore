using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CalmSpace.Editor
{
    /// <summary>
    /// Synthesizes the palette of close, tactile placement sounds the room is
    /// tidied with. Everything is generated from scratch here — a short noise
    /// transient shaped by a few damped resonances — so the project carries no
    /// third-party sample.
    ///
    /// One identical click on every placement is what makes a tidying game
    /// feel mechanical. These materials give the pooled audio service enough
    /// variety that repeated placements read as physical rather than looped.
    /// </summary>
    public static class CalmSpaceTactileAudioBuilder
    {
        public const string AudioRoot = "Assets/CalmSpace/Audio/Tactile";

        private const int SampleRate = 44100;
        private const short Channels = 1;
        private const short BitsPerSample = 16;

        /// <summary>
        /// One struck material: a noise transient, a damped body, and how
        /// quickly the whole thing gets out of the way.
        /// </summary>
        private readonly struct Material
        {
            public Material(
                string name,
                float[] partials,
                float decay,
                float noiseDecay,
                float noiseMix,
                float brightness,
                uint seed)
            {
                Name = name;
                Partials = partials;
                Decay = decay;
                NoiseDecay = noiseDecay;
                NoiseMix = noiseMix;
                Brightness = brightness;
                Seed = seed;
            }

            public string Name { get; }
            public float[] Partials { get; }
            public float Decay { get; }
            public float NoiseDecay { get; }
            public float NoiseMix { get; }
            public float Brightness { get; }
            public uint Seed { get; }
        }

        private static readonly Material[] Materials =
        {
            // Soft blocks and folded cloth: almost no pitch, mostly air.
            new Material(
                "SoftCloth",
                new[] { 148f, 262f, 406f },
                30f, 120f, 0.46f, 0.16f, 0x5EED01u),
            // A wooden drawer or shelf taking weight.
            new Material(
                "WarmWood",
                new[] { 184f, 431f, 902f, 1370f },
                22f, 150f, 0.30f, 0.30f, 0x5EED02u),
            // Trail stones settling against each other.
            new Material(
                "RiverStone",
                new[] { 712f, 1490f, 2610f },
                38f, 220f, 0.26f, 0.62f, 0x5EED03u),
            // Glazed jars and tea tins.
            new Material(
                "GlazedClay",
                new[] { 928f, 2104f, 3390f, 4620f },
                15f, 240f, 0.18f, 0.78f, 0x5EED04u),
            // A fastener dropped into its compartment.
            new Material(
                "SmallMetal",
                new[] { 1640f, 3120f, 5210f },
                26f, 260f, 0.22f, 0.90f, 0x5EED05u),
            // The bench itself: low, reassuring, the heaviest of the set.
            new Material(
                "HeavyBench",
                new[] { 96f, 174f, 288f, 512f },
                14f, 100f, 0.34f, 0.10f, 0x5EED06u)
        };

        /// <summary>
        /// Regenerates the palette and returns the clips in a stable order.
        /// Safe to run repeatedly: identical input produces identical bytes,
        /// and an unchanged file is left alone so its importer settings and
        /// GUID survive.
        /// </summary>
        public static AudioClip[] CreateOrUpdate()
        {
            EnsureFolder(AudioRoot);
            var clips = new List<AudioClip>(Materials.Length);
            foreach (Material material in Materials)
            {
                string path = AudioRoot + "/" + material.Name + ".wav";
                byte[] wave = BuildStrike(material);
                WriteIfChanged(path, wave);

                AudioClip clip =
                    AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null)
                {
                    throw new InvalidOperationException(
                        "Could not import generated clip " + path);
                }

                ConfigureImport(path);
                clips.Add(clip);
            }

            return clips.ToArray();
        }

        private static byte[] BuildStrike(Material material)
        {
            // Long enough for the slowest body to fall silent.
            int sampleCount = Mathf.CeilToInt(SampleRate * 0.42f);
            int dataLength = sampleCount * Channels * (BitsPerSample / 8);

            using (var stream = new MemoryStream(44 + dataLength))
            using (var writer = new BinaryWriter(stream))
            {
                WriteHeader(writer, dataLength);

                uint noiseState = material.Seed;
                var lowPassA = 0f;
                var lowPassB = 0f;
                var peak = 0f;
                var samples = new float[sampleCount];

                // Two cascaded one-pole sections. A single pole leaves far too
                // much high end, which made every material read as the same
                // bright hiss regardless of its body.
                float cutoff = Mathf.Lerp(0.015f, 0.28f, material.Brightness);
                // Cascading loses level; put it back so the mix stays honest.
                float noiseGain = 2.4f / Mathf.Max(cutoff, 0.02f) * 0.06f;

                for (var index = 0; index < sampleCount; index++)
                {
                    float time = index / (float)SampleRate;

                    // Contact transient: filtered noise that dies fast.
                    noiseState ^= noiseState << 13;
                    noiseState ^= noiseState >> 17;
                    noiseState ^= noiseState << 5;
                    float white =
                        (noiseState & 0xFFFF) / 32768f - 1f;
                    lowPassA += (white - lowPassA) * cutoff;
                    lowPassB += (lowPassA - lowPassB) * cutoff;
                    float transient = lowPassB * noiseGain *
                        Mathf.Exp(-time * material.NoiseDecay);

                    // Body: a few damped partials, higher ones dying sooner.
                    var body = 0f;
                    for (var partial = 0;
                         partial < material.Partials.Length;
                         partial++)
                    {
                        float frequency = material.Partials[partial];
                        float amplitude = 1f / (partial + 1.6f);
                        float damping = material.Decay *
                            (1f + partial * 0.65f);
                        body += amplitude *
                            Mathf.Sin(2f * Mathf.PI * frequency * time) *
                            Mathf.Exp(-time * damping);
                    }

                    body /= material.Partials.Length;

                    // A 3 ms fade-in keeps the attack from clicking.
                    float attack = Mathf.Clamp01(time / 0.003f);
                    float value = attack * Mathf.Lerp(
                        body, transient, material.NoiseMix);
                    samples[index] = value;
                    peak = Mathf.Max(peak, Mathf.Abs(value));
                }

                // Normalize to a consistent, unclipped level, then fade the
                // tail so no clip ends on a discontinuity.
                float gain = peak > 0.0001f ? 0.82f / peak : 0f;
                int fadeStart = sampleCount - SampleRate / 100;
                for (var index = 0; index < sampleCount; index++)
                {
                    float value = samples[index] * gain;
                    if (index >= fadeStart)
                    {
                        value *= (sampleCount - index) /
                            (float)(sampleCount - fadeStart);
                    }

                    writer.Write(ToPcm16(value));
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        private static void ConfigureImport(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is AudioImporter importer))
            {
                return;
            }

            AudioImporterSampleSettings settings =
                importer.defaultSampleSettings;
            bool changed =
                importer.forceToMono != true ||
                importer.loadInBackground ||
                settings.loadType != AudioClipLoadType.DecompressOnLoad ||
                settings.compressionFormat !=
                    AudioCompressionFormat.PCM;

            if (!changed)
            {
                return;
            }

            // Short one-shots: decompressed and resident, so a placement never
            // waits on streaming.
            importer.forceToMono = true;
            importer.loadInBackground = false;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        private static void WriteIfChanged(string path, byte[] wave)
        {
            string full = Path.GetFullPath(path);
            if (File.Exists(full) &&
                ByteArraysEqual(File.ReadAllBytes(full), wave))
            {
                return;
            }

            File.WriteAllBytes(full, wave);
            AssetDatabase.ImportAsset(
                path,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
        }

        private static bool ByteArraysEqual(byte[] left, byte[] right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            for (var index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }

        private static short ToPcm16(float sample)
        {
            return (short)Mathf.RoundToInt(
                Mathf.Clamp(sample, -1f, 1f) * short.MaxValue);
        }

        private static void WriteHeader(
            BinaryWriter writer,
            int dataLength)
        {
            const int bytesPerSample = BitsPerSample / 8;
            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataLength);
            writer.Write(new[] { 'W', 'A', 'V', 'E' });
            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write(Channels);
            writer.Write(SampleRate);
            writer.Write(SampleRate * Channels * bytesPerSample);
            writer.Write((short)(Channels * bytesPerSample));
            writer.Write(BitsPerSample);
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataLength);
        }
    }
}
