using Microsoft.Extensions.Logging;
using SIPSorceryMedia.Abstractions;

namespace WebRTC.Server
{
    public class FFmpegStreamSource: IAudioSource, IDisposable
    {
        private static ILogger logger = SIPSorcery.LogFactory.CreateLogger<FFmpegStreamSource>();

        private FFmpegAudioStreamSource ? _FFmpegAudioSource = null;

        private bool _isStarted;
        private bool _isPaused;
        private bool _isClosed;

        public event EncodedSampleDelegate? OnAudioSourceEncodedSample;
        public event RawAudioSampleDelegate? OnAudioSourceRawSample;

        public event SourceErrorDelegate? OnAudioSourceError;

        public unsafe FFmpegStreamSource(Stream stream, bool repeat, IAudioEncoder? audioEncoder, uint audioFrameSize = 960)
        {
            
            if ((audioEncoder != null))
            {
                _FFmpegAudioSource = new FFmpegAudioStreamSource(audioEncoder, audioFrameSize);
                _FFmpegAudioSource.CreateAudioDecoder(stream, null, repeat, false);

                _FFmpegAudioSource.OnAudioSourceEncodedSample += _FFmpegAudioSource_OnAudioSourceEncodedSample;
                _FFmpegAudioSource.OnAudioSourceRawSample += _FFmpegAudioSource_OnAudioSourceRawSample;
                _FFmpegAudioSource.OnAudioSourceError += _FFmpegAudioSource_OnAudioSourceError;
            }

        }

        private void _FFmpegAudioSource_OnAudioSourceError(string errorMessage)
        {
            OnAudioSourceError?.Invoke(errorMessage);
        }

        private void _FFmpegAudioSource_OnAudioSourceEncodedSample(uint durationRtpUnits, byte[] sample)
        {
            OnAudioSourceEncodedSample?.Invoke(durationRtpUnits, sample);
        }

        private void _FFmpegAudioSource_OnAudioSourceRawSample(AudioSamplingRatesEnum samplingRate, uint durationMilliseconds, short[] sample)
        {
            OnAudioSourceRawSample?.Invoke(samplingRate, durationMilliseconds, sample);

        }

        public bool IsPaused() => _isPaused;

        public List<AudioFormat> GetAudioSourceFormats()
        {
            if (_FFmpegAudioSource != null)
                return _FFmpegAudioSource.GetAudioSourceFormats();
            return new List<AudioFormat>();
        }
        public void SetAudioSourceFormat(AudioFormat audioFormat)
        {
            if (_FFmpegAudioSource != null)
            {
                logger.LogDebug($"Setting audio source format to {audioFormat.FormatID}:{audioFormat.Codec} {audioFormat.ClockRate}.");
                _FFmpegAudioSource.SetAudioSourceFormat(audioFormat);
            }
        }
        public void RestrictFormats(Func<AudioFormat, bool> filter)
        {
            if (_FFmpegAudioSource != null)
                _FFmpegAudioSource.RestrictFormats(filter);
        }
        public void ExternalAudioSourceRawSample(AudioSamplingRatesEnum samplingRate, uint durationMilliseconds, short[] sample) => throw new NotImplementedException();
        
        public bool HasEncodedAudioSubscribers()
        {
            Boolean result = OnAudioSourceEncodedSample != null;
            if (_FFmpegAudioSource != null)
            {
                if (result)
                    _FFmpegAudioSource.OnAudioSourceEncodedSample += _FFmpegAudioSource_OnAudioSourceEncodedSample;
                else
                    _FFmpegAudioSource.OnAudioSourceEncodedSample -= _FFmpegAudioSource_OnAudioSourceEncodedSample;
            }

            return result;
        }

        public bool HasRawAudioSubscribers()
        {
            Boolean result = OnAudioSourceRawSample!= null;
            if (_FFmpegAudioSource != null)
            {
                if (result)
                    _FFmpegAudioSource.OnAudioSourceRawSample += _FFmpegAudioSource_OnAudioSourceRawSample;
                else
                    _FFmpegAudioSource.OnAudioSourceRawSample -= _FFmpegAudioSource_OnAudioSourceRawSample;
            }

            return result;
        }

        public bool IsAudioSourcePaused() => _isPaused;
        public Task StartAudio() => Start();
        public Task PauseAudio() => Pause();
        public Task ResumeAudio() => Resume();
        public Task CloseAudio() => Close();

       

        
        public void ExternalVideoSourceRawSample(uint durationMilliseconds, int width, int height, byte[] sample, VideoPixelFormatsEnum pixelFormat) => throw new NotImplementedException();
        public void ExternalVideoSourceRawSampleFaster(uint durationMilliseconds, RawImage rawImage) => throw new NotImplementedException();

        public bool IsVideoSourcePaused() => _isPaused;
        public Task StartVideo() => Start();
        public Task PauseVideo() => Pause();
        public Task ResumeVideo() => Resume();
        public Task CloseVideo() => Close();

        public Task Start()
        {
            if (!_isStarted)
            {
                _isStarted = true;
                _FFmpegAudioSource?.Start();
            }

            return Task.CompletedTask;
        }

        public async Task Close()
        {
            if (!_isClosed)
            {
                _isClosed = true;

                if (_FFmpegAudioSource != null)
                    await _FFmpegAudioSource.Close();

                Dispose();
            }
        }

        public async Task Pause()
        {
            if (!_isPaused)
            {
                _isPaused = true;

                if (_FFmpegAudioSource != null)
                    await _FFmpegAudioSource.Pause();
            }
        }

        public async Task Resume()
        {
            if (_isPaused && !_isClosed)
            {
                _isPaused = false;

                if (_FFmpegAudioSource != null)
                    await _FFmpegAudioSource.Resume();
            }
        }

        public void Dispose()
        {
            if (_FFmpegAudioSource != null)
            {
                _FFmpegAudioSource.OnAudioSourceEncodedSample -= _FFmpegAudioSource_OnAudioSourceEncodedSample;
                _FFmpegAudioSource.OnAudioSourceRawSample -= _FFmpegAudioSource_OnAudioSourceRawSample;
                _FFmpegAudioSource.OnAudioSourceError -= _FFmpegAudioSource_OnAudioSourceError;

                _FFmpegAudioSource.Dispose();

                _FFmpegAudioSource = null;
            }
        }
    }
}