using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace CalmSpace.Cleaning
{
    [DisallowMultipleComponent]
    public sealed class RenderTextureCleaner : MonoBehaviour
    {
        /// <summary>
        /// Alpha-weighted coverage that counts as done. Deliberately short of
        /// 1.0 so one missed pixel never strands the player on a surface that
        /// already looks clean.
        /// </summary>
        public const float CompletionThreshold = 0.95f;

        private const int ReadbackSlotCount = 2;
        private const int MinimumResolution = 16;
        private const int MaximumResolution = 1024;
        private const int MaximumInterpolatedStampsPerInput = 32;
        private const float StrokeDiscontinuityDistanceUv = 0.35f;
        private const string DefaultMaskTextureProperty = "_CleanMask";

        private static readonly int SourceMaskId =
            Shader.PropertyToID("_SourceMask");
        private static readonly int BrushUvRadiusHardnessId =
            Shader.PropertyToID("_BrushUvRadiusHardness");
        private static readonly int CoverageSampleStepUvId =
            Shader.PropertyToID("_CoverageSampleStepUv");

        [Header("Scene references")]
        [SerializeField]
        private Camera raycastCamera;

        [SerializeField]
        private MeshCollider cleanSurfaceCollider;

        [SerializeField]
        private Renderer visibleRenderer;

        [Tooltip("Keep this explicit reference so the hidden shader is included in builds.")]
        [SerializeField]
        private Shader maskBrushShader;

        [Tooltip("Explicit reference prevents the coverage shader from being stripped.")]
        [SerializeField]
        private Shader coverageDownsampleShader;

        [Header("Mask")]
        [SerializeField]
        private Vector2Int maskResolution = new Vector2Int(512, 512);

        [Tooltip("Low-resolution mask used only for progress evaluation.")]
        [SerializeField]
        private Vector2Int coverageResolution = new Vector2Int(64, 64);

        [SerializeField]
        private string maskTextureProperty = DefaultMaskTextureProperty;

        [Range(0.001f, 0.5f)]
        [SerializeField]
        private float brushRadiusUv = 0.055f;

        [Range(0f, 1f)]
        [SerializeField]
        private float brushHardness = 0.8f;

        [Tooltip("Distance between interpolated stamps as a fraction of the brush radius.")]
        [Range(0.05f, 1f)]
        [SerializeField]
        private float strokeSpacingRadiusFraction = 0.35f;

        [Min(0.01f)]
        [SerializeField]
        private float progressSampleIntervalSeconds = 0.35f;

        [Tooltip("Minimum rendered frames between coverage evaluations.")]
        [Min(1)]
        [SerializeField]
        private int progressSampleFrameInterval = 10;

        [Min(0.01f)]
        [SerializeField]
        private float raycastDistance = 1000f;

        private readonly CleanCompletionLatch _completionLatch =
            new CleanCompletionLatch(CompletionThreshold);

        private RenderTexture _frontMask;
        private RenderTexture _backMask;
        private RenderTexture _coverageMask;
        private Material _brushMaterial;
        private Material _coverageDownsampleMaterial;
        private MaterialPropertyBlock _brushProperties;
        private MaterialPropertyBlock _coverageProperties;
        private MaterialPropertyBlock _visibleProperties;
        private MaterialPropertyBlock _originalVisibleProperties;
        private bool _originalVisiblePropertiesWereEmpty;
        private NativeArray<byte> _cpuShadowMask;
        private ReadbackSlot[] _readbackSlots;
        private CancellationTokenSource _lifetimeCancellation;
        private GraphicsFormat _maskGraphicsFormat;
        private int _maskChannelStride;
        private int _maskTexturePropertyId;
        private int _coveragePixelCount;
        private int _generation;
        private long _nextSampleSequence;
        private long _lastAppliedSampleSequence;
        private CoverageSampleGate _sampleGate;
        private bool _gpuReadbackEnabled;
        private bool _initialized;
        private bool _configurationErrorLogged;
        private bool _readbackErrorLogged;
        private bool _visibleRendererBound;
        private bool _raycastCameraResolutionAttempted;
        private bool _hasPreviousStrokeUv;
        private Vector2 _previousStrokeUv;

        public event Action<float> CleanProgressChanged;

        public event Action OnCleaned100Percent;

        /// <summary>
        /// Alpha-weighted visual clean coverage. The linear cleanable-surface
        /// shader consumes the same value, so soft brush edges contribute
        /// proportionally rather than through an arbitrary binary cutoff.
        /// </summary>
        public float CleanedFraction { get; private set; }

        public RenderTexture MaskTexture => _frontMask;

        public GraphicsFormat MaskGraphicsFormat => _maskGraphicsFormat;

        public bool IsInitialized => _initialized;

        public bool IsUsingAsyncGpuReadback => _gpuReadbackEnabled;

        public Vector2Int CoverageResolution => coverageResolution;

        public int ProgressSampleFrameInterval =>
            progressSampleFrameInterval;

        private void OnEnable()
        {
            ResolveRaycastCameraIfNeeded();
            TryInitialize();
        }

        private void OnDisable()
        {
            Teardown();
        }

        private void OnDestroy()
        {
            Teardown();
        }

        private void OnValidate()
        {
            maskResolution = new Vector2Int(
                Mathf.Clamp(maskResolution.x, MinimumResolution, MaximumResolution),
                Mathf.Clamp(maskResolution.y, MinimumResolution, MaximumResolution));
            coverageResolution = new Vector2Int(
                Mathf.Clamp(
                    coverageResolution.x,
                    MinimumResolution,
                    maskResolution.x),
                Mathf.Clamp(
                    coverageResolution.y,
                    MinimumResolution,
                    maskResolution.y));
            brushRadiusUv = Mathf.Clamp(brushRadiusUv, 0.001f, 0.5f);
            brushHardness = Mathf.Clamp01(brushHardness);
            strokeSpacingRadiusFraction =
                Mathf.Clamp(strokeSpacingRadiusFraction, 0.05f, 1f);
            progressSampleIntervalSeconds =
                Mathf.Max(0.01f, progressSampleIntervalSeconds);
            progressSampleFrameInterval =
                Mathf.Max(1, progressSampleFrameInterval);
            raycastDistance = Mathf.Max(0.01f, raycastDistance);
        }

        public bool Initialize()
        {
            ResolveRaycastCameraIfNeeded();
            return TryInitialize();
        }

        /// <summary>
        /// Swaps the brush profile so one cleaner can stand in for a
        /// different implement. Values are clamped to the same range the
        /// inspector enforces.
        /// </summary>
        public void ConfigureBrush(
            float radiusUv,
            float hardness)
        {
            if (!float.IsNaN(radiusUv))
            {
                brushRadiusUv = Mathf.Clamp(radiusUv, 0.001f, 0.5f);
            }

            if (!float.IsNaN(hardness))
            {
                brushHardness = Mathf.Clamp01(hardness);
            }
        }

        public bool PaintFromScreenPoint(Vector2 screenPoint)
        {
            if (!TryInitialize() ||
                raycastCamera == null ||
                cleanSurfaceCollider == null)
            {
                EndStroke();
                return false;
            }

            Ray ray = raycastCamera.ScreenPointToRay(screenPoint);

            if (!cleanSurfaceCollider.Raycast(
                    ray,
                    out RaycastHit hit,
                    raycastDistance))
            {
                EndStroke();
                return false;
            }

            StampUv(hit.textureCoord);
            return true;
        }

        public void BeginStroke()
        {
            _hasPreviousStrokeUv = false;
        }

        public void EndStroke()
        {
            _hasPreviousStrokeUv = false;
        }

        public void StampUv(Vector2 uv)
        {
            if (!TryInitialize())
            {
                return;
            }

            Vector2 clampedUv = new Vector2(
                Mathf.Clamp01(uv.x),
                Mathf.Clamp01(uv.y));

            if (!_hasPreviousStrokeUv)
            {
                StampSingleUv(clampedUv);
                _previousStrokeUv = clampedUv;
                _hasPreviousStrokeUv = true;
                return;
            }

            float minimumTexelSize = Mathf.Max(
                1f / maskResolution.x,
                1f / maskResolution.y);
            float spacing = Mathf.Max(
                minimumTexelSize,
                brushRadiusUv * strokeSpacingRadiusFraction);
            float distance = Vector2.Distance(_previousStrokeUv, clampedUv);
            if (distance > StrokeDiscontinuityDistanceUv)
            {
                // UV atlas seams and resumed/missed pointers must not paint a
                // long line across unrelated islands or issue hundreds of
                // full-screen ping-pong passes in a single frame.
                StampSingleUv(clampedUv);
                _previousStrokeUv = clampedUv;
                return;
            }

            int requiredStepCount = Mathf.Max(
                1,
                Mathf.CeilToInt(distance / spacing));
            if (requiredStepCount >
                MaximumInterpolatedStampsPerInput)
            {
                StampSingleUv(clampedUv);
                _previousStrokeUv = clampedUv;
                return;
            }

            int stepCount = requiredStepCount;

            for (int step = 1; step <= stepCount; step++)
            {
                float interpolation = step / (float)stepCount;
                StampSingleUv(
                    Vector2.LerpUnclamped(
                        _previousStrokeUv,
                        clampedUv,
                        interpolation));
            }

            _previousStrokeUv = clampedUv;
        }

        public void ResetMask()
        {
            if (!TryInitialize())
            {
                return;
            }

            if (!EnsureRenderTexturesCreated())
            {
                return;
            }

            AdvanceGeneration();
            ClearRenderTextures();
            ClearCpuShadowMask();
            BindFrontMaskToVisibleRenderer();
            CleanedFraction = 0f;
            _completionLatch.Reset();
            _nextSampleSequence = 0L;
            _lastAppliedSampleSequence = 0L;
            _sampleGate?.Reset();
            EndStroke();
        }

        public UniTask<float> EvaluateProgressAsync()
        {
            return EvaluateProgressAsync(CancellationToken.None);
        }

        public async UniTask<float> EvaluateProgressAsync(
            CancellationToken cancellationToken)
        {
            if (!TryInitialize())
            {
                return CleanedFraction;
            }

            if (!EnsureRenderTexturesCreated())
            {
                return CleanedFraction;
            }

            int sampleGeneration = _generation;
            CancellationToken lifetimeToken = _lifetimeCancellation.Token;

            using (var linkedCancellation =
                   CancellationTokenSource.CreateLinkedTokenSource(
                       cancellationToken,
                       lifetimeToken))
            {
                CancellationToken token = linkedCancellation.Token;

                if (!await ReserveSampleWindowAsync(
                        sampleGeneration,
                        token))
                {
                    return CleanedFraction;
                }

                long sequence = ++_nextSampleSequence;
                ReadbackSlot slot = await AcquireReadbackSlotAsync(
                    sampleGeneration,
                    token);

                if (slot == null)
                {
                    return CleanedFraction;
                }

                slot.BeginLease(sequence, sampleGeneration);

                try
                {
                    int channelStride = 1;
                    bool copiedGpuPixels = false;

                    if (_gpuReadbackEnabled)
                    {
                        copiedGpuPixels = await TryCopyGpuReadbackAsync(
                            slot,
                            sampleGeneration,
                            token);
                    }

                    token.ThrowIfCancellationRequested();

                    if (!_initialized || sampleGeneration != _generation)
                    {
                        return CleanedFraction;
                    }

                    if (copiedGpuPixels)
                    {
                        channelStride = _maskChannelStride;
                    }
                    else
                    {
                        CopyCpuShadowTo(slot);
                    }

                    slot.ScheduleSum(
                        _coveragePixelCount,
                        channelStride);

                    while (!slot.IsSumCompleted)
                    {
                        token.ThrowIfCancellationRequested();
                        await UniTask.Yield(PlayerLoopTiming.Update, token);
                    }

                    token.ThrowIfCancellationRequested();

                    // The handle is completed only after IsCompleted has been
                    // observed. This keeps evaluation non-blocking in normal use.
                    slot.CompleteFinishedSum();

                    if (!_initialized ||
                        sampleGeneration != _generation ||
                        slot.Generation != sampleGeneration ||
                        slot.Sequence != sequence)
                    {
                        return CleanedFraction;
                    }

                    float fraction = CalculateFraction(slot.SumValue);
                    return ApplyCompletedSample(
                        fraction,
                        sequence,
                        sampleGeneration);
                }
                finally
                {
                    // Cancellation or teardown can occur after scheduling. At
                    // this disposal boundary the persistent slot cannot be
                    // released until its GPU request and job no longer own it.
                    slot.CompleteRequestForRelease();
                    slot.CompleteSumForRelease();
                    slot.ReleaseLease();
                }
            }
        }

        private bool TryInitialize()
        {
            if (_initialized)
            {
                return true;
            }

            if (!ValidateConfiguration())
            {
                return false;
            }

            int width = Mathf.Clamp(
                maskResolution.x,
                MinimumResolution,
                MaximumResolution);
            int height = Mathf.Clamp(
                maskResolution.y,
                MinimumResolution,
                MaximumResolution);
            maskResolution = new Vector2Int(width, height);
            int coverageWidth = Mathf.Clamp(
                coverageResolution.x,
                MinimumResolution,
                width);
            int coverageHeight = Mathf.Clamp(
                coverageResolution.y,
                MinimumResolution,
                height);
            coverageResolution =
                new Vector2Int(coverageWidth, coverageHeight);
            _coveragePixelCount =
                checked(coverageWidth * coverageHeight);

            if (!TryChooseMaskFormat(
                    out _maskGraphicsFormat,
                    out _maskChannelStride))
            {
                LogConfigurationError(
                    "Neither R8_UNorm nor R8G8B8A8_UNorm can be rendered and sampled on this device.");
                return false;
            }

            _maskTexturePropertyId = Shader.PropertyToID(
                string.IsNullOrWhiteSpace(maskTextureProperty)
                    ? DefaultMaskTextureProperty
                    : maskTextureProperty);

            try
            {
                _lifetimeCancellation = new CancellationTokenSource();
                _brushMaterial = CoreUtils.CreateEngineMaterial(maskBrushShader);
                _coverageDownsampleMaterial =
                    CoreUtils.CreateEngineMaterial(
                        coverageDownsampleShader);
                _brushProperties = new MaterialPropertyBlock();
                _coverageProperties = new MaterialPropertyBlock();
                _visibleProperties = new MaterialPropertyBlock();
                _originalVisibleProperties =
                    new MaterialPropertyBlock();
                _frontMask = CreateMaskRenderTexture(
                    "Clean Mask A",
                    maskResolution);
                _backMask = CreateMaskRenderTexture(
                    "Clean Mask B",
                    maskResolution);
                _coverageMask = CreateMaskRenderTexture(
                    "Clean Coverage 64",
                    coverageResolution);

                if (_brushMaterial == null ||
                    _coverageDownsampleMaterial == null ||
                    _frontMask == null ||
                    _backMask == null ||
                    _coverageMask == null)
                {
                    throw new InvalidOperationException(
                        "Failed to create cleaning render resources.");
                }

                _cpuShadowMask = new NativeArray<byte>(
                    _coveragePixelCount,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory);

                int slotByteCapacity = checked(
                    _coveragePixelCount * _maskChannelStride);
                _readbackSlots = new ReadbackSlot[ReadbackSlotCount];

                for (int index = 0; index < _readbackSlots.Length; index++)
                {
                    _readbackSlots[index] =
                        new ReadbackSlot(slotByteCapacity);
                }

                _gpuReadbackEnabled =
                    SystemInfo.supportsAsyncGPUReadback &&
                    SupportsReadPixels(_maskGraphicsFormat);
                _readbackErrorLogged = false;
                _configurationErrorLogged = false;
                _initialized = true;
                AdvanceGeneration();
                CleanedFraction = 0f;
                _completionLatch.Reset();
                _nextSampleSequence = 0L;
                _lastAppliedSampleSequence = 0L;
                _sampleGate = new CoverageSampleGate(
                    progressSampleFrameInterval,
                    progressSampleIntervalSeconds);
                EndStroke();
                ClearRenderTextures();
                CaptureAndBindVisibleRenderer();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"RenderTextureCleaner could not initialize: {exception.Message}",
                    this);
                Teardown();
                return false;
            }
        }

        private void ResolveRaycastCameraIfNeeded()
        {
            if (raycastCamera != null ||
                _raycastCameraResolutionAttempted)
            {
                return;
            }

            // Addressable prefabs cannot serialize a reference to the camera in
            // the persistent scene. Resolve it once when the component becomes
            // active, while preserving an explicitly assigned test/editor
            // reference and avoiding camera lookup during input or Update.
            _raycastCameraResolutionAttempted = true;
            raycastCamera = Camera.main;
        }

        private bool ValidateConfiguration()
        {
            if (maskBrushShader == null)
            {
                LogConfigurationError(
                    "Assign the Hidden/CalmSpace/MaskBrush shader reference.");
                return false;
            }

            if (coverageDownsampleShader == null)
            {
                LogConfigurationError(
                    "Assign the Hidden/CalmSpace/CoverageDownsample shader reference.");
                return false;
            }

            if (!maskBrushShader.isSupported)
            {
                LogConfigurationError(
                    $"The assigned brush shader '{maskBrushShader.name}' is not supported.");
                return false;
            }

            if (!coverageDownsampleShader.isSupported)
            {
                LogConfigurationError(
                    $"The assigned coverage shader '{coverageDownsampleShader.name}' is not supported.");
                return false;
            }

            return true;
        }

        private void LogConfigurationError(string message)
        {
            if (_configurationErrorLogged)
            {
                return;
            }

            Debug.LogError($"RenderTextureCleaner: {message}", this);
            _configurationErrorLogged = true;
        }

        private static bool TryChooseMaskFormat(
            out GraphicsFormat graphicsFormat,
            out int channelStride)
        {
            GraphicsFormat r8Format = GraphicsFormat.R8_UNorm;

            if (SupportsMaskFormat(r8Format, requireReadPixels: false))
            {
                graphicsFormat = r8Format;
                channelStride = 1;
                return true;
            }

            GraphicsFormat rgbaFormat = GraphicsFormat.R8G8B8A8_UNorm;

            if (SupportsMaskFormat(rgbaFormat, requireReadPixels: false))
            {
                graphicsFormat = rgbaFormat;
                channelStride = 4;
                return true;
            }

            graphicsFormat = GraphicsFormat.None;
            channelStride = 0;
            return false;
        }

        private static bool SupportsMaskFormat(
            GraphicsFormat graphicsFormat,
            bool requireReadPixels)
        {
            bool supported =
                SupportsRender(graphicsFormat) &&
                SupportsSampling(graphicsFormat);

            if (requireReadPixels)
            {
                supported &= SupportsReadPixels(graphicsFormat);
            }

            return supported;
        }

        private static bool SupportsRender(GraphicsFormat graphicsFormat)
        {
#if UNITY_6000_0_OR_NEWER
            return SystemInfo.IsFormatSupported(
                graphicsFormat,
                GraphicsFormatUsage.Render);
#else
            return SystemInfo.IsFormatSupported(
                graphicsFormat,
                FormatUsage.Render);
#endif
        }

        private static bool SupportsSampling(GraphicsFormat graphicsFormat)
        {
#if UNITY_6000_0_OR_NEWER
            return SystemInfo.IsFormatSupported(
                graphicsFormat,
                GraphicsFormatUsage.Sample);
#else
            return SystemInfo.IsFormatSupported(
                graphicsFormat,
                FormatUsage.Sample);
#endif
        }

        private static bool SupportsReadPixels(GraphicsFormat graphicsFormat)
        {
#if UNITY_6000_0_OR_NEWER
            return SystemInfo.IsFormatSupported(
                graphicsFormat,
                GraphicsFormatUsage.ReadPixels);
#else
            return SystemInfo.IsFormatSupported(
                graphicsFormat,
                FormatUsage.ReadPixels);
#endif
        }

        private RenderTexture CreateMaskRenderTexture(
            string textureName,
            Vector2Int resolution)
        {
            var descriptor = new RenderTextureDescriptor(
                resolution.x,
                resolution.y,
                _maskGraphicsFormat,
                0)
            {
                dimension = TextureDimension.Tex2D,
                volumeDepth = 1,
                msaaSamples = 1,
                mipCount = 1,
                useMipMap = false,
                autoGenerateMips = false,
                enableRandomWrite = false,
                bindMS = false,
                useDynamicScale = false,
                memoryless = RenderTextureMemoryless.None
            };

            var renderTexture = new RenderTexture(descriptor)
            {
                name = textureName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0,
                hideFlags = HideFlags.DontSave
            };

            if (renderTexture.Create())
            {
                return renderTexture;
            }

            CoreUtils.Destroy(renderTexture);
            return null;
        }

        private void ClearRenderTextures()
        {
            if (_frontMask == null ||
                _backMask == null ||
                _coverageMask == null)
            {
                return;
            }

            CommandBuffer commandBuffer =
                CommandBufferPool.Get("CalmSpace.ClearCleanMask");

            try
            {
                CoreUtils.SetRenderTarget(
                    commandBuffer,
                    _frontMask,
                    ClearFlag.Color,
                    Color.clear);
                CoreUtils.SetRenderTarget(
                    commandBuffer,
                    _coverageMask,
                    ClearFlag.Color,
                    Color.clear);
                CoreUtils.SetRenderTarget(
                    commandBuffer,
                    _backMask,
                    ClearFlag.Color,
                    Color.clear);
                Graphics.ExecuteCommandBuffer(commandBuffer);
            }
            finally
            {
                CommandBufferPool.Release(commandBuffer);
            }
        }

        private void StampSingleUv(Vector2 uv)
        {
            if (!EnsureRenderTexturesCreated())
            {
                return;
            }

            _brushProperties.Clear();
            _brushProperties.SetTexture(SourceMaskId, _frontMask);
            _brushProperties.SetVector(
                BrushUvRadiusHardnessId,
                new Vector4(
                    uv.x,
                    uv.y,
                    brushRadiusUv,
                    brushHardness));

            CommandBuffer commandBuffer =
                CommandBufferPool.Get("CalmSpace.StampCleanMask");

            try
            {
                CoreUtils.SetRenderTarget(
                    commandBuffer,
                    _backMask,
                    ClearFlag.None);
                CoreUtils.DrawFullScreen(
                    commandBuffer,
                    _brushMaterial,
                    _brushProperties,
                    0);
                Graphics.ExecuteCommandBuffer(commandBuffer);
            }
            finally
            {
                CommandBufferPool.Release(commandBuffer);
            }

            RenderTexture previousFront = _frontMask;
            _frontMask = _backMask;
            _backMask = previousFront;

            StampCpuShadow(uv);
            BindFrontMaskToVisibleRenderer();
        }

        private void StampCpuShadow(Vector2 uv)
        {
            float radius = brushRadiusUv;
            float innerRadius = radius * brushHardness;
            int width = coverageResolution.x;
            int height = coverageResolution.y;
            int minimumX = Mathf.Max(
                0,
                Mathf.FloorToInt((uv.x - radius) * width - 0.5f));
            int maximumX = Mathf.Min(
                width - 1,
                Mathf.CeilToInt((uv.x + radius) * width - 0.5f));
            int minimumY = Mathf.Max(
                0,
                Mathf.FloorToInt((uv.y - radius) * height - 0.5f));
            int maximumY = Mathf.Min(
                height - 1,
                Mathf.CeilToInt((uv.y + radius) * height - 0.5f));

            for (int y = minimumY; y <= maximumY; y++)
            {
                float pixelUvY = (y + 0.5f) / height;

                for (int x = minimumX; x <= maximumX; x++)
                {
                    float pixelUvX = (x + 0.5f) / width;
                    float distance = Vector2.Distance(
                        new Vector2(pixelUvX, pixelUvY),
                        uv);
                    float alpha = CalculateBrushAlpha(
                        distance,
                        innerRadius,
                        radius);

                    if (alpha <= 0f)
                    {
                        continue;
                    }

                    byte value = (byte)Mathf.Clamp(
                        Mathf.RoundToInt(alpha * byte.MaxValue),
                        byte.MinValue,
                        byte.MaxValue);
                    int pixelIndex = y * width + x;

                    if (value > _cpuShadowMask[pixelIndex])
                    {
                        _cpuShadowMask[pixelIndex] = value;
                    }
                }
            }
        }

        private static float CalculateBrushAlpha(
            float distance,
            float innerRadius,
            float outerRadius)
        {
            if (distance >= outerRadius)
            {
                return 0f;
            }

            if (distance <= innerRadius ||
                outerRadius - innerRadius <= Mathf.Epsilon)
            {
                return 1f;
            }

            float interpolation = Mathf.Clamp01(
                (distance - innerRadius) /
                (outerRadius - innerRadius));
            float smooth = interpolation * interpolation *
                           (3f - 2f * interpolation);
            return 1f - smooth;
        }

        private async UniTask<bool> ReserveSampleWindowAsync(
            int sampleGeneration,
            CancellationToken token)
        {
            while (_initialized && sampleGeneration == _generation)
            {
                token.ThrowIfCancellationRequested();
                if (_sampleGate.TryReserve(
                        Time.frameCount,
                        Time.realtimeSinceStartupAsDouble))
                {
                    return true;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            return false;
        }

        private async UniTask<ReadbackSlot> AcquireReadbackSlotAsync(
            int sampleGeneration,
            CancellationToken token)
        {
            while (_initialized && sampleGeneration == _generation)
            {
                token.ThrowIfCancellationRequested();

                for (int index = 0; index < _readbackSlots.Length; index++)
                {
                    ReadbackSlot slot = _readbackSlots[index];

                    if (!slot.IsLeased)
                    {
                        return slot;
                    }
                }

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            return null;
        }

        private async UniTask<bool> TryCopyGpuReadbackAsync(
            ReadbackSlot slot,
            int sampleGeneration,
            CancellationToken token)
        {
            try
            {
                _coverageProperties.Clear();
                _coverageProperties.SetTexture(
                    SourceMaskId,
                    _frontMask);
                _coverageProperties.SetVector(
                    CoverageSampleStepUvId,
                    new Vector4(
                        1f / (4f * coverageResolution.x),
                        1f / (4f * coverageResolution.y),
                        0f,
                        0f));

                CommandBuffer commandBuffer =
                    CommandBufferPool.Get(
                        "CalmSpace.DownsampleCleanCoverage");
                try
                {
                    CoreUtils.SetRenderTarget(
                        commandBuffer,
                        _coverageMask,
                        ClearFlag.None);
                    CoreUtils.DrawFullScreen(
                        commandBuffer,
                        _coverageDownsampleMaterial,
                        _coverageProperties,
                        0);
                    Graphics.ExecuteCommandBuffer(commandBuffer);
                }
                finally
                {
                    CommandBufferPool.Release(commandBuffer);
                }

                slot.BeginGpuReadback(
                    AsyncGPUReadback.Request(
                        _coverageMask,
                        0,
                        _maskGraphicsFormat,
                        null));
            }
            catch (Exception exception)
            {
                DisableGpuReadback(exception.Message);
                return false;
            }

            while (!slot.Request.done)
            {
                token.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            token.ThrowIfCancellationRequested();
            slot.MarkGpuReadbackComplete();

            if (!_initialized || sampleGeneration != _generation)
            {
                return false;
            }

            if (slot.Request.hasError)
            {
                DisableGpuReadback("The GPU readback request returned an error.");
                return false;
            }

            try
            {
                NativeArray<byte> requestPixels =
                    slot.Request.GetData<byte>();
                int expectedByteCount = checked(
                    _coveragePixelCount * _maskChannelStride);

                if (requestPixels.Length < expectedByteCount)
                {
                    DisableGpuReadback(
                        $"GPU readback returned {requestPixels.Length} bytes; expected {expectedByteCount}.");
                    return false;
                }

                // UUM-102552: the request-owned array is a one-frame view.
                // Copy it immediately into our persistent slot before the job
                // is scheduled; the job never retains request-owned memory.
                NativeArray<byte>.Copy(
                    requestPixels,
                    slot.Pixels,
                    expectedByteCount);
                return true;
            }
            catch (Exception exception)
            {
                DisableGpuReadback(exception.Message);
                return false;
            }
        }

        private void DisableGpuReadback(string reason)
        {
            _gpuReadbackEnabled = false;

            if (_readbackErrorLogged)
            {
                return;
            }

            Debug.LogWarning(
                $"RenderTextureCleaner is using its CPU shadow mask because async GPU readback failed: {reason}",
                this);
            _readbackErrorLogged = true;
        }

        private void CopyCpuShadowTo(ReadbackSlot slot)
        {
            NativeArray<byte>.Copy(
                _cpuShadowMask,
                slot.Pixels,
                _coveragePixelCount);
        }

        private float CalculateFraction(long sum)
        {
            double maximumSum =
                (double)_coveragePixelCount * byte.MaxValue;

            if (maximumSum <= 0d)
            {
                return 0f;
            }

            return Mathf.Clamp01((float)(sum / maximumSum));
        }

        private float ApplyCompletedSample(
            float fraction,
            long sequence,
            int sampleGeneration)
        {
            if (!_initialized ||
                sampleGeneration != _generation ||
                sequence <= _lastAppliedSampleSequence)
            {
                return CleanedFraction;
            }

            _lastAppliedSampleSequence = sequence;
            CleanedFraction = fraction;
            bool completedNow = _completionLatch.TryComplete(fraction);
            Action<float> progressHandler = CleanProgressChanged;
            Action completionHandler = OnCleaned100Percent;

            progressHandler?.Invoke(fraction);

            if (completedNow)
            {
                completionHandler?.Invoke();
            }

            return fraction;
        }

        private void CaptureAndBindVisibleRenderer()
        {
            if (visibleRenderer == null)
            {
                return;
            }

            visibleRenderer.GetPropertyBlock(
                _originalVisibleProperties);
            _originalVisiblePropertiesWereEmpty =
                _originalVisibleProperties.isEmpty;
            _visibleRendererBound = true;
            BindFrontMaskToVisibleRenderer();
        }

        private void BindFrontMaskToVisibleRenderer()
        {
            if (!_visibleRendererBound || visibleRenderer == null)
            {
                return;
            }

            visibleRenderer.GetPropertyBlock(_visibleProperties);
            _visibleProperties.SetTexture(
                _maskTexturePropertyId,
                _frontMask);
            visibleRenderer.SetPropertyBlock(_visibleProperties);
        }

        private void RestoreVisibleRenderer()
        {
            if (!_visibleRendererBound || visibleRenderer == null)
            {
                _visibleRendererBound = false;
                return;
            }

            visibleRenderer.SetPropertyBlock(
                _originalVisiblePropertiesWereEmpty
                    ? null
                    : _originalVisibleProperties);
            _visibleRendererBound = false;
            _originalVisiblePropertiesWereEmpty = false;
        }

        private bool EnsureRenderTexturesCreated()
        {
            if (_frontMask == null ||
                _backMask == null ||
                _coverageMask == null)
            {
                return false;
            }

            if (_frontMask.IsCreated() &&
                _backMask.IsCreated() &&
                _coverageMask.IsCreated())
            {
                return true;
            }

            if (_readbackSlots != null)
            {
                for (var index = 0;
                     index < _readbackSlots.Length;
                     index++)
                {
                    if (_readbackSlots[index].IsLeased)
                    {
                        return false;
                    }
                }
            }

            AdvanceGeneration();
            if ((!_frontMask.IsCreated() && !_frontMask.Create()) ||
                (!_backMask.IsCreated() && !_backMask.Create()) ||
                (!_coverageMask.IsCreated() &&
                 !_coverageMask.Create()))
            {
                return false;
            }

            Texture2D stagingTexture = null;
            NativeArray<byte> rgbaPixels = default;

            try
            {
                stagingTexture = new Texture2D(
                    coverageResolution.x,
                    coverageResolution.y,
                    _maskGraphicsFormat,
                    TextureCreationFlags.None)
                {
                    hideFlags = HideFlags.DontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };

                if (_maskChannelStride == 1)
                {
                    stagingTexture.SetPixelData(
                        _cpuShadowMask,
                        0);
                }
                else
                {
                    rgbaPixels = new NativeArray<byte>(
                        checked(
                            _coveragePixelCount *
                            _maskChannelStride),
                        Allocator.Temp,
                        NativeArrayOptions.ClearMemory);
                    for (var index = 0;
                         index < _coveragePixelCount;
                         index++)
                    {
                        rgbaPixels[index * _maskChannelStride] =
                            _cpuShadowMask[index];
                    }

                    stagingTexture.SetPixelData(rgbaPixels, 0);
                }

                stagingTexture.Apply(false, false);
                Graphics.Blit(stagingTexture, _frontMask);
                Graphics.Blit(stagingTexture, _backMask);
                Graphics.Blit(stagingTexture, _coverageMask);
                BindFrontMaskToVisibleRenderer();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"RenderTextureCleaner could not restore a lost mask: " +
                    exception.Message,
                    this);
                return false;
            }
            finally
            {
                if (rgbaPixels.IsCreated)
                {
                    rgbaPixels.Dispose();
                }

                if (stagingTexture != null)
                {
                    CoreUtils.Destroy(stagingTexture);
                }
            }
        }

        private void ClearCpuShadowMask()
        {
            if (!_cpuShadowMask.IsCreated)
            {
                return;
            }

            for (int index = 0; index < _cpuShadowMask.Length; index++)
            {
                _cpuShadowMask[index] = 0;
            }
        }

        private void AdvanceGeneration()
        {
            unchecked
            {
                _generation++;
            }
        }

        private void Teardown()
        {
            if (!_initialized &&
                _frontMask == null &&
                _backMask == null &&
                _coverageMask == null &&
                _brushMaterial == null &&
                _coverageDownsampleMaterial == null &&
                !_cpuShadowMask.IsCreated &&
                _readbackSlots == null &&
                _lifetimeCancellation == null)
            {
                return;
            }

            AdvanceGeneration();
            _initialized = false;
            EndStroke();

            if (_lifetimeCancellation != null)
            {
                _lifetimeCancellation.Cancel();
                _lifetimeCancellation.Dispose();
                _lifetimeCancellation = null;
            }

            RestoreVisibleRenderer();

            if (_readbackSlots != null)
            {
                for (int index = 0; index < _readbackSlots.Length; index++)
                {
                    _readbackSlots[index]?.Dispose();
                }

                _readbackSlots = null;
            }

            if (_cpuShadowMask.IsCreated)
            {
                _cpuShadowMask.Dispose();
                _cpuShadowMask = default;
            }

            ReleaseRenderTexture(ref _frontMask);
            ReleaseRenderTexture(ref _backMask);
            ReleaseRenderTexture(ref _coverageMask);

            if (_brushMaterial != null)
            {
                CoreUtils.Destroy(_brushMaterial);
                _brushMaterial = null;
            }

            if (_coverageDownsampleMaterial != null)
            {
                CoreUtils.Destroy(_coverageDownsampleMaterial);
                _coverageDownsampleMaterial = null;
            }

            _brushProperties = null;
            _coverageProperties = null;
            _visibleProperties = null;
            _originalVisibleProperties = null;
            _gpuReadbackEnabled = false;
            _coveragePixelCount = 0;
            _maskChannelStride = 0;
            _maskGraphicsFormat = GraphicsFormat.None;
            _nextSampleSequence = 0L;
            _lastAppliedSampleSequence = 0L;
            _sampleGate = null;
            CleanedFraction = 0f;
            _completionLatch.Reset();
        }

        private static void ReleaseRenderTexture(
            ref RenderTexture renderTexture)
        {
            if (renderTexture == null)
            {
                return;
            }

            if (renderTexture.IsCreated())
            {
                renderTexture.Release();
            }

            CoreUtils.Destroy(renderTexture);
            renderTexture = null;
        }

        private sealed class ReadbackSlot : IDisposable
        {
            private NativeArray<byte> _pixels;
            private NativeArray<long> _sum;
            private JobHandle _sumHandle;
            private bool _requestPending;
            private bool _sumScheduled;
            private bool _disposed;

            public ReadbackSlot(int byteCapacity)
            {
                _pixels = new NativeArray<byte>(
                    byteCapacity,
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);
                _sum = new NativeArray<long>(
                    1,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory);
            }

            public NativeArray<byte> Pixels => _pixels;

            public AsyncGPUReadbackRequest Request { get; set; }

            public bool IsLeased { get; private set; }

            public long Sequence { get; private set; }

            public int Generation { get; private set; }

            public bool IsSumCompleted =>
                _sumScheduled && _sumHandle.IsCompleted;

            public long SumValue => _sum.IsCreated ? _sum[0] : 0L;

            public void BeginLease(long sequence, int generation)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(ReadbackSlot));
                }

                if (IsLeased)
                {
                    throw new InvalidOperationException(
                        "The readback slot is already leased.");
                }

                IsLeased = true;
                Sequence = sequence;
                Generation = generation;
                Request = default;
            }

            public void BeginGpuReadback(
                AsyncGPUReadbackRequest request)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(ReadbackSlot));
                }

                Request = request;
                _requestPending = true;
            }

            public void MarkGpuReadbackComplete()
            {
                if (!_requestPending || !Request.done)
                {
                    throw new InvalidOperationException(
                        "The GPU readback request has not completed yet.");
                }

                _requestPending = false;
            }

            public void ScheduleSum(
                int pixelCount,
                int channelStride)
            {
                _sum[0] = 0L;
                _sumHandle = new SumMaskJob
                {
                    Pixels = _pixels,
                    Sum = _sum,
                    PixelCount = pixelCount,
                    ChannelStride = channelStride
                }.Schedule();
                _sumScheduled = true;
            }

            public void CompleteFinishedSum()
            {
                if (!_sumScheduled || !_sumHandle.IsCompleted)
                {
                    throw new InvalidOperationException(
                        "The sum job has not completed yet.");
                }

                _sumHandle.Complete();
                _sumScheduled = false;
            }

            public void CompleteSumForRelease()
            {
                if (!_sumScheduled)
                {
                    return;
                }

                // Teardown/cancellation is the only blocking path. Persistent
                // memory cannot be released while a job still owns it.
                _sumHandle.Complete();
                _sumScheduled = false;
            }

            public void CompleteRequestForRelease()
            {
                if (!_requestPending)
                {
                    return;
                }

                // AsyncGPUReadback has no cancellation API. Waiting is limited
                // to cancellation/teardown so its source RenderTexture remains
                // alive until the GPU has stopped using it.
                if (!Request.done)
                {
                    Request.WaitForCompletion();
                }

                _requestPending = false;
            }

            public void ReleaseLease()
            {
                if (_requestPending || _sumScheduled)
                {
                    throw new InvalidOperationException(
                        "A readback slot cannot be released while work is pending.");
                }

                IsLeased = false;
                Sequence = 0L;
                Generation = 0;
                Request = default;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                CompleteRequestForRelease();
                CompleteSumForRelease();

                if (_sum.IsCreated)
                {
                    _sum.Dispose();
                    _sum = default;
                }

                if (_pixels.IsCreated)
                {
                    _pixels.Dispose();
                    _pixels = default;
                }

                IsLeased = false;
                _disposed = true;
            }
        }
    }
}
